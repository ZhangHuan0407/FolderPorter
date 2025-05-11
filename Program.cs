using FolderPorter.Model;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Text.Json;

namespace FolderPorter
{
    internal static class Program
    {
        internal const int VerifyBufferLength = 1024;
        internal const int SyncFilePreTurn = 8;
        internal const int SliceLength = 1024 * 1024;

        internal const string ServerType = "server";
        internal const string PullType = "pull";
        internal const string PushType = "push";

        internal readonly static UnixFileMode DirectoryUnixFileMode = UnixFileMode.UserRead |
                                                                      UnixFileMode.UserWrite |
                                                                      UnixFileMode.UserExecute |
                                                                      UnixFileMode.GroupRead |
                                                                      UnixFileMode.GroupWrite |
                                                                      UnixFileMode.GroupExecute |
                                                                      UnixFileMode.OtherRead |
                                                                      UnixFileMode.OtherExecute;
        internal readonly static UnixFileMode FileUnixFileMode = UnixFileMode.UserRead |
                                                                 UnixFileMode.UserWrite |
                                                                 UnixFileMode.UserExecute |
                                                                 UnixFileMode.GroupRead |
                                                                 UnixFileMode.GroupWrite |
                                                                 UnixFileMode.GroupExecute |
                                                                 UnixFileMode.OtherRead |
                                                                 UnixFileMode.OtherExecute;

        private static readonly object m_Lock = new object();
        private static Guid m_RunningTask;

        [STAThread]
        private static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            GC.AddMemoryPressure(20 * 1024 * 1024); // os socket and file cache

            Console.WriteLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            Console.WriteLine($"DateTime: {DateTime.Now}");

            AppSettingModel.Reload();
            ThreadPool.SetMaxThreads(AppSettingModel.Instance.MaxWorkerThreadCount,
                                     AppSettingModel.Instance.MaxIOThreadCount);
            if (AppSettingModel.IsTemplate)
                AppSettingModel.CopyAppSettings();

            ArgumentModel.ParseArgs(args);
            ArgumentModel argsModel = ArgumentModel.Instance;
            if (argsModel.Help)
                ArgumentModel.LogHelp();
            if (argsModel.InteractiveMode)
                argsModel.EnterInteractiveMode();

            // server listern port
            if (argsModel.Server)
            {
                ServerMode();
            }
            // push folder to server
            else if (!string.IsNullOrEmpty(argsModel.PushRemoteDrive))
            {
                PushMode();
            }
            else if (!string.IsNullOrEmpty(argsModel.PullRemoteDrive))
            {
                PullMode();
            }
        }

        #region Server
        private static TcpListener ServerMode()
        {
            Console.WriteLine($"ServerMode, ListernPort: {AppSettingModel.Instance.ListernPort}");
            using TcpListener tcpListener = TcpListener.Create(AppSettingModel.Instance.ListernPort);
            tcpListener.Start(4);
            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                Guid taskGuid = Guid.NewGuid();
                Console.WriteLine($"Guid: {taskGuid}, IP: {tcpClient.Client.RemoteEndPoint}");
                _ = Task.Run(async () =>
                            {
                                try
                                {
                                    AppSettingModel.Instance.SetTcpClientParameter(tcpClient);
                                    await VerifyLocalPasswordAsync(tcpClient);
                                    NetworkStream networkStream = tcpClient.GetStream();
                                    // if user have changed AppSettings.json, it will hot reload
                                    AppSettingModel.Reload();

                                    OptionRequestModel requestModel = await ReadOptionAsync(networkStream, taskGuid);
                                    Console.WriteLine($"Guid: {taskGuid}, Folder: {requestModel.Folder}, Type: {requestModel.Type}");
                                    if (requestModel.Type == PullType)
                                    {
                                        FolderModel folderModel = AppSettingModel.Instance.LocalFolders[requestModel.Folder];
                                        await PushFolderAsync(networkStream, folderModel, tokenSource.Token);
                                    }
                                    else if (requestModel.Type == PushType)
                                        await AcceptPushFolderAsync(tcpClient);
                                    else
                                        throw new Exception($"Unknown type: {requestModel.Type}");
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine(tcpClient.Client.RemoteEndPoint);
                                    Console.Error.WriteLine(ex);
                                }
                                finally
                                {
                                    lock (m_Lock)
                                    {
                                        if (m_RunningTask == taskGuid)
                                            m_RunningTask = Guid.Empty;
                                    }
                                    tcpClient.Dispose();
                                    tokenSource.Cancel();
                                    tokenSource.Dispose();
                                }
                                Console.WriteLine($"Guid: {taskGuid} run to end.");
                                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                                GC.Collect();
                            });
            }
        }

        private static async Task VerifyLocalPasswordAsync(TcpClient tcpClient)
        {
            NetworkStream networkStream = tcpClient.GetStream();
            byte[] bytes = new byte[VerifyBufferLength];
            int pointer = 0;
            await networkStream.ReadExactlyAsync(bytes, pointer, 4);
            int strLength = ByteEncoder.ReadInt(bytes, ref pointer);
            await networkStream.ReadExactlyAsync(bytes, pointer, strLength);
            pointer = 0;
            string password = ByteEncoder.ReadString(bytes, ref pointer);
            if (password != AppSettingModel.Instance.Password)
                throw new Exception("verify failed");
            bytes[0] = 1;
            await networkStream.WriteAsync(bytes, 0, 1);
            Console.WriteLine("VerifyLocalPassword success");
        }

        private static async Task<OptionRequestModel> ReadOptionAsync(NetworkStream networkStream, Guid taskGuid)
        {
            byte[] buffer = new byte[VerifyBufferLength];
            while (true)
            {
                OptionRequestModel requestModel = await ReadModelAsync<OptionRequestModel>(networkStream, buffer);
                OptionResponseModel responseModel = new OptionResponseModel(Assembly.GetExecutingAssembly().GetName().Version!);
                responseModel.FindFolder = AppSettingModel.Instance.LocalFolders.TryGetValue(requestModel.Folder, out FolderModel folderModel);
                if (folderModel != null)
                {
                    if (requestModel.Type == PullType &&
                        !folderModel.CanRead)
                        responseModel.Refause = true;
                    else if (requestModel.Type == PushType &&
                        !folderModel.CanWrite)
                        responseModel.Refause = true;
                }
                
                lock (m_Lock)
                {
                    if (!responseModel.Refause &&
                        m_RunningTask == Guid.Empty)
                        m_RunningTask = taskGuid;
                    else
                        responseModel.PleaseWaiting = true;
                }
                WriteModel(networkStream, buffer, responseModel);
                if (!responseModel.PleaseWaiting)
                    return requestModel;
            }
        }

        private static async Task AcceptPushFolderAsync(TcpClient tcpClient)
        {
            await Task.CompletedTask;
            NetworkStream networkStream = tcpClient.GetStream();

            byte[] crc32Buffer = null;
            byte[] buffer = new byte[SliceLength + 1024];
            int pointer = 0;

            Dictionary<int, FileSliceHashModel> fileIndexToModel = new Dictionary<int, FileSliceHashModel>();
            int turn = 0;
        ReadNextTurn:
            turn++;
            if (turn % 50 == 49)
                GC.Collect();

            // read PushFolderRequestModel
            PushFolderRequestModel requestModel = await ReadModelAsync<PushFolderRequestModel>(networkStream, buffer);
            if (!AppSettingModel.Instance.LocalFolders.TryGetValue(requestModel.Folder, out FolderModel folderModel))
            {
                tcpClient.Client.Disconnect(false);
                return;
            }

            // compare file slice
            if (requestModel.FileSliceHashList.Count == 0)
                goto SkipCompareFileSlice;
            PushFolderResponseModel responseModel = new PushFolderResponseModel();
            for (int i = 0; i < requestModel.FileSliceHashList.Count; i++)
            {
                FileSliceHashModel fileSliceHashModel = requestModel.FileSliceHashList[i];
                fileIndexToModel[fileSliceHashModel.FileIndex] = fileSliceHashModel;
                string fileFullPath = $"{folderModel.RootPath}/{fileSliceHashModel.FileRelativePath}";
                FileInfo fileInfo = new FileInfo(fileFullPath);
                fileFullPath = fileInfo.FullName.Replace('\\', '/');
                if (!fileFullPath.StartsWith(folderModel.RootPath + "/"))
                    throw new Exception($"FileRelativePath: {fileSliceHashModel.FileRelativePath}, FileFullPath: {fileFullPath}");
                FileSliceHashModel localModel;
                if (!fileInfo.Exists)
                {
                    localModel = null;
                    Directory.CreateDirectory(fileInfo.DirectoryName, DirectoryUnixFileMode);
                    fileInfo.Create().Dispose();
                    fileInfo.UnixFileMode = FileUnixFileMode;
                }
                else
                {
                    localModel = new FileSliceHashModel(fileSliceHashModel.FileIndex,
                                                        fileSliceHashModel.FileRelativePath,
                                                        fileInfo,
                                                        ref crc32Buffer);
                    if (localModel.FileTotalLength != fileSliceHashModel.FileTotalLength)
                    {
                        using (FileStream fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Write))
                            fileStream.SetLength(fileSliceHashModel.FileTotalLength);
                    }
                }

                for (int sliceIndex = 0; sliceIndex < fileSliceHashModel.CRCList.Count; sliceIndex++)
                {
                    if (localModel != null &&
                        localModel.CRCList.Count > sliceIndex &&
                        localModel.CRCList[sliceIndex] == fileSliceHashModel.CRCList[sliceIndex])
                        continue;
                    responseModel.NeedSyncList.Add(new FileAnchor(fileSliceHashModel.FileIndex, sliceIndex));
                }
            }
            WriteModel(networkStream, buffer, responseModel);
            goto ReadNextTurn;
        SkipCompareFileSlice:

            // read and write file slice
            if (requestModel.BytesTransferList.Count == 0)
                goto SkipTransferBytes;
            for (int i = 0; i < requestModel.BytesTransferList.Count; i++)
            {
                FileAnchor fileAnchor = requestModel.BytesTransferList[i];
                FileSliceHashModel fileSliceHashModel = fileIndexToModel[fileAnchor.FileIndex];

                pointer = 0;
                await networkStream.ReadExactlyAsync(buffer, pointer, 4);
                int bytesLength = ByteEncoder.ReadInt(buffer, ref pointer);
                pointer = 0;
                await networkStream.ReadExactlyAsync(buffer, pointer, bytesLength);
                string fileFullPath = $"{folderModel.RootPath}/{fileSliceHashModel.FileRelativePath}";
                using (FileStream fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    int start = fileAnchor.SliceIndex * SliceLength;
                    fileStream.Position = start;
                    await fileStream.WriteAsync(buffer, 0, bytesLength);
                }
            }
            goto ReadNextTurn;
        SkipTransferBytes:

            if (!requestModel.TransferFinish)
                goto SkipTransferFinish;
            HashSet<string> fileRelativePathSet = new HashSet<string>();
            foreach (FileSliceHashModel fileSliceHashModel in fileIndexToModel.Values)
                fileRelativePathSet.Add(fileSliceHashModel.FileRelativePath);
            PushFolderResponseModel transferFinishResponseModel = new PushFolderResponseModel();
            transferFinishResponseModel.DeleteFilesCount = folderModel.CleanFifthWheelFiles(fileRelativePathSet);
            folderModel.CleanEmptyDirectory();
            WriteModel(networkStream, buffer, transferFinishResponseModel);
        SkipTransferFinish:

            ;
        }
        #endregion

        #region Push
        private static void PushMode()
        {
            Console.WriteLine($"PushMode");
            ArgumentModel argsModel = ArgumentModel.Instance;
            if (!AppSettingModel.Instance.RemoteDevice.TryGetValue(argsModel.PushRemoteDrive, out RemoteDeviceModel? remoteDeviceModel) ||
                !AppSettingModel.Instance.LocalFolders.TryGetValue(argsModel.PushFolder, out FolderModel? folderModel))
                throw new Exception($"Not found drive:{argsModel.PushRemoteDrive} or not found folder: {argsModel.PushFolder}");
            if (!folderModel.CanRead)
                throw new Exception($"FolderModel.CanRead => {folderModel.CanRead}");
            if (!Directory.Exists(folderModel.RootPath))
                throw new DirectoryNotFoundException($"Folder: {folderModel.Folder}, RootPath: {folderModel.RootPath}");

            using TcpClient tcpClient = remoteDeviceModel.TryConnect();
            if (tcpClient == null)
                throw new Exception($"Can not connect to {remoteDeviceModel.DeviceName}");
            using NetworkStream networkStream = tcpClient.GetStream();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();

            Task task = Task.Run(async () =>
            {
                try
                {
                    await VerifyRemotePasswordAsync(networkStream, remoteDeviceModel.DevicePassword);
                    await SendOptionAsync(networkStream, PushType, folderModel.Folder);
                    await PushFolderAsync(networkStream, folderModel, tokenSource.Token);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            });

            Task.WaitAll(task);
            tokenSource.Cancel();
        }

        private static async Task PushFolderAsync(NetworkStream networkStream, FolderModel folderModel, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            // calculate file hash in task
            Queue<FileSliceHashModel> waitingSyncFileList = new Queue<FileSliceHashModel>();
            Task calculateFileHashTask = CalculateFileSliceHashAsync(folderModel, waitingSyncFileList, cancellationToken);
            long transferTotalBytes = 0L;
            Stopwatch transferStopwatch = Stopwatch.StartNew();

            byte[] buffer = new byte[SliceLength + 1024];
            int pointer = 0;
            int turn = 0;
            while (calculateFileHashTask.Status == TaskStatus.WaitingForActivation ||
                calculateFileHashTask.Status == TaskStatus.WaitingToRun ||
                calculateFileHashTask.Status == TaskStatus.Running ||
                waitingSyncFileList.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                turn++;
                if (turn % 50 == 49)
                    GC.Collect();

                // copy FileSliceHashModel to PushFolderRequestModel
                PushFolderRequestModel requestModel = null;
                lock (waitingSyncFileList)
                {
                    if (waitingSyncFileList.Count == 0)
                        goto WaitAndTryAgain;
                    requestModel = new PushFolderRequestModel(folderModel.Folder);
                    long sumFileSize = 0L;
                    while (waitingSyncFileList.Count > 0 &&
                        requestModel.FileSliceHashList.Count < SyncFilePreTurn &&
                        sumFileSize < 100 * SliceLength)
                    {
                        FileSliceHashModel model = waitingSyncFileList.Dequeue();
                        sumFileSize += model.FileTotalLength;
                        requestModel.FileSliceHashList.Add(model);
                    }
                }
                WriteModel(networkStream, buffer, requestModel);

                // read PushFolderResponseModel
                PushFolderResponseModel responseModel = await ReadModelAsync<PushFolderResponseModel>(networkStream, buffer);
                if (responseModel.NeedSyncList.Count == 0)
                    continue;
                List<FileSliceHashModel> fileSliceHashList = requestModel.FileSliceHashList;

                // send PushFolderRequestModel.BytesTransferList
                {
                    requestModel = new PushFolderRequestModel(folderModel.Folder);
                    requestModel.BytesTransferList.AddRange(responseModel.NeedSyncList);
                    WriteModel(networkStream, buffer, requestModel);
                }

                // send byte[]
                for (int i = 0; i < responseModel.NeedSyncList.Count; i++)
                {
                    FileAnchor fileAnchor = responseModel.NeedSyncList[i];
                    FileSliceHashModel fileSliceHashModel = fileSliceHashList.First(model => model.FileIndex == fileAnchor.FileIndex);
                    long start = fileAnchor.SliceIndex * SliceLength;
                    int length = (int)Math.Min(fileSliceHashModel.FileTotalLength - start, SliceLength);
                    string fileFullPath = $"{folderModel.RootPath}/{fileSliceHashModel.FileRelativePath}";
                    using FileStream fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read);
                    fileStream.Position = start;
                    pointer = 4;
                    await fileStream.ReadExactlyAsync(buffer, pointer, length);
                    pointer = 0;
                    ByteEncoder.WriteInt(buffer, length, ref pointer);
                    await networkStream.WriteAsync(buffer, 0, length + 4);
                    transferTotalBytes += length;
                    LogTransferState(i, responseModel.NeedSyncList.Count, transferTotalBytes, transferStopwatch,
                                     fileSliceHashModel.FileRelativePath);
                }
                Console.Write('\n');

                continue;
            WaitAndTryAgain:
                await Task.Delay(50);
            }

            if (cancellationToken.IsCancellationRequested)
                return;
            // send finish flag, and waiting server send back finish flag
            PushFolderRequestModel transferFinishRequestModel = new PushFolderRequestModel(folderModel.Folder)
            {
                TransferFinish = true
            };
            WriteModel(networkStream, buffer, transferFinishRequestModel);
            string transferTime;
            if (transferStopwatch.Elapsed.TotalHours > 2f)
                transferTime = transferStopwatch.Elapsed.TotalHours.ToString("0.0") + " h";
            else
                transferTime = transferStopwatch.Elapsed.TotalMinutes.ToString("0.0") + " min";
            Console.WriteLine($"Have send {(transferTotalBytes / 1024f / 1024f):0.00} mb bytes in {transferTime}");
            Console.WriteLine("Waiting for remote ensure transfer finish...");
            PushFolderResponseModel transferFinishResponseModel = await ReadModelAsync<PushFolderResponseModel>(networkStream, buffer);
            Console.WriteLine($"Remote delete files count: {transferFinishResponseModel.DeleteFilesCount}");

            if (calculateFileHashTask.Exception != null)
                throw calculateFileHashTask.Exception;
        }

        private static Task CalculateFileSliceHashAsync(FolderModel folderModel, Queue<FileSliceHashModel> waitingSyncFileList,
                                                        CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                int fileIndex = 0;
                byte[] crcBuffer = null;
                foreach ((string fileRelativePath, FileInfo fileInfo) in folderModel.EnumFiles())
                {
                    fileIndex++;
                    FileSliceHashModel sliceHashModel = new FileSliceHashModel(fileIndex, fileRelativePath, fileInfo, ref crcBuffer);
                    int count;
                    lock (waitingSyncFileList)
                    {
                        waitingSyncFileList.Enqueue(sliceHashModel);
                        count = waitingSyncFileList.Count;
                    }
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    if (count > SyncFilePreTurn * 10)
                        await Task.Delay(50);
                    else if (count > SyncFilePreTurn * 4)
                        await Task.Delay(5);
                }
            });
        }

        private static string m_LastFileRelativePath;
        private static void LogTransferState(int i, int total, long transferTotalLength, Stopwatch stopwatch, string fileRelativePath)
        {
            if (ArgumentModel.Instance.Server)
                return;
            if (i > 0 &&
                m_LastFileRelativePath == fileRelativePath)
                Console.Write("\r");
            m_LastFileRelativePath = fileRelativePath;
            double speed = transferTotalLength / stopwatch.Elapsed.TotalSeconds;
            string unit;
            if (speed > 1000000)
            {
                speed = speed / 1024 / 1024;
                unit = "mb/s";
            }
            else if (speed > 1000)
            {
                speed = speed / 1024;
                unit = "kb/s";
            }
            else
                unit = "b/s";
            string rightPart = $"Slice:[{i.ToString().PadLeft(2)}/{total.ToString().PadLeft(2)}] {speed:0.00} {unit}";
            int maxLeftLength = Math.Clamp(Console.WindowWidth - rightPart.Length, 0, 255);
            string leftPart;
            if (fileRelativePath.Length <= maxLeftLength)
                leftPart = fileRelativePath;
            else
            {
                int trimLength = fileRelativePath.Length - maxLeftLength + 3;
                trimLength = Math.Max(trimLength, 0);
                leftPart = "..." + fileRelativePath.Substring(trimLength);
            }
            string logLine = leftPart.PadRight(maxLeftLength) + rightPart;
            Console.Write(logLine);
        }
        #endregion

        #region Pull
        private static void PullMode()
        {
            Console.WriteLine($"PullMode");
            ArgumentModel argsModel = ArgumentModel.Instance;
            if (!AppSettingModel.Instance.RemoteDevice.TryGetValue(argsModel.PullRemoteDrive, out RemoteDeviceModel? remoteDeviceModel) ||
                !AppSettingModel.Instance.LocalFolders.TryGetValue(argsModel.PullFolder, out FolderModel? folderModel))
                throw new Exception($"Not found drive:{argsModel.PushRemoteDrive} or not found folder: {argsModel.PushFolder}");
            if (!folderModel.CanWrite)
                throw new Exception($"FolderModel.CanWrite => {folderModel.CanWrite}");

            using TcpClient tcpClient = remoteDeviceModel.TryConnect();
            if (tcpClient == null)
                throw new Exception($"Can not connect to {remoteDeviceModel.DeviceName}");
            using NetworkStream networkStream = tcpClient.GetStream();
            using CancellationTokenSource tokenSource = new CancellationTokenSource();

            Task task = Task.Run(async () =>
            {
                try
                {
                    await VerifyRemotePasswordAsync(networkStream, remoteDeviceModel.DevicePassword);
                    await SendOptionAsync(networkStream, PullType, folderModel.Folder);
                    await AcceptPushFolderAsync(tcpClient);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            });

            Task.WaitAll(task);
            tokenSource.Cancel();
        }
        #endregion

        private static async Task VerifyRemotePasswordAsync(NetworkStream networkStream, string devicePassword)
        {
            byte[] bytes = new byte[VerifyBufferLength];
            int pointer = 0;
            ByteEncoder.WriteString(bytes, devicePassword, ref pointer);
            await networkStream.WriteAsync(bytes, 0, pointer);

            int remoteResult = networkStream.ReadByte();
            if (remoteResult != 1)
                throw new Exception($"VerityPassword failed with: {remoteResult}");
            Console.WriteLine("VerifyRemotePassword success");
        }

        private static async Task SendOptionAsync(NetworkStream networkStream, string type, string folder)
        {
            byte[] buffer = new byte[VerifyBufferLength];
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            while (true)
            {
                OptionRequestModel requestModel = new OptionRequestModel(type, folder);
                WriteModel(networkStream, new byte[VerifyBufferLength], requestModel);
                OptionResponseModel responseModel = await ReadModelAsync<OptionResponseModel>(networkStream, buffer);
                if (!responseModel.FindFolder)
                    throw new Exception($"Remote not find folder: {folder}");
                if (responseModel.Refause)
                    throw new Exception($"Remote refause {type}");
                if (version != responseModel.RemoteVersion)
                {
                    Console.WriteLine($"Warning! current app version: {version}, but remote version: {responseModel.RemoteVersion}");
                    Console.WriteLine("Suggest to stop app, but you can press any key to continue...");
                    Console.ReadLine();
                    version = responseModel.RemoteVersion;
                }
                if (!responseModel.PleaseWaiting)
                    break;
                await Task.Delay(AppSettingModel.Instance.RemoteBuzyRetryTimeSpan);
            }
        }

        private static void WriteModel<TModel>(NetworkStream networkStream, byte[] buffer, TModel model)
        {
            string modelStr = JsonSerializer.Serialize(model);
            int pointer = 0;
            ByteEncoder.WriteString(buffer, modelStr, ref pointer);
            networkStream.Write(buffer, 0, pointer);
            if (AppSettingModel.Instance.LogProtocal)
            {
                Console.WriteLine(model!.GetType().FullName);
                Console.WriteLine(modelStr);
            }
            networkStream.Flush();
        }

        private static async Task<TModel> ReadModelAsync<TModel>(NetworkStream networkStream, byte[] buffer)
        {
            int pointer = 0;
            await networkStream.ReadExactlyAsync(buffer, pointer, 4);
            int modelStrLength = ByteEncoder.ReadInt(buffer, ref pointer);
            await networkStream.ReadExactlyAsync(buffer, pointer, modelStrLength);
            pointer = 0;
            string modelStr = ByteEncoder.ReadString(buffer, ref pointer);
            if (AppSettingModel.Instance.LogProtocal)
            {
                Console.WriteLine(typeof(TModel).FullName);
                Console.WriteLine(modelStr);
            }
            TModel requestModel = JsonSerializer.Deserialize<TModel>(modelStr)!;
            return requestModel;
        }
    }
}
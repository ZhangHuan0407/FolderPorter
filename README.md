Folder movers use TCP links to synchronize folders between multiple computers, transferring data differentially.

# Linux install
```
cd /var
unzip FolderPorter.zip
cd /lib
mkdir FolderPorter
mv /var/FolderPorter/* /lib/FolderPorter/
cp AppSettingsTemplate.json AppSettings.json
nano AppSettings.json
```

# update AppSettings.json
```
{
  "Password": "c7ce0d8e-4985-4464-9146-0767be889a45",
  "LocalFolders": {
    "RegexGameWebGL": {
      "RootPath": "D:\\RegexGame\\Builds\\WebGL Github\\RegexGame",
      "CanWrite": true,
      "CanRead": true
    }
  },
  "RemoteDevice": {
    "raspberry": {
      "IP": "192.168.1.3:17979",
      "DevicePassword": "d0d642fb-b77d-4e32-b77d-2444cd8788c3"
    }
  },

  "MaxWorkerThreadCount": 2,
  "MaxIOThreadCount": 3,

  "RemoteBuzyRetrySeconds": 5,
  "ConnectTimeoutSeconds": 30,

  "ListernPort": 17979
}

```
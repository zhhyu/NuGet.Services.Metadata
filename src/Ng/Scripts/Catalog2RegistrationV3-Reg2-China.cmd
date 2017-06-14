@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2registrationv3reg2China.Title}"

title #{Jobs.catalog2registrationv3reg2China.Title}

start /w ng.exe catalog2registration ^
    -source #{Jobs.common.v3.Source} ^
    -contentBaseAddress #{Jobs.catalog2registrationv3reg2China.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.catalog2registrationv3reg2China.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.v3China.StorageAccountName} ^
    -storageKeyValue #{Jobs.common.v3ChinaStorage.Key} ^
    -storageContainer #{Jobs.catalog2registrationv3reg2.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg2.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg2China.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.v3China.StorageAccountName} ^
    -compressedStorageKeyValue #{Jobs.common.v3ChinaStorage.Key} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg2.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg2.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.catalog2registrationv3reg2China.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.v3China.StorageAccountName} ^
    -semVer2StorageKeyValue #{Jobs.common.v3ChinaStorage.Key} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg2.StorageContainerSemVer2}

echo "Finished #{Jobs.catalog2registrationv3reg2China.Title}"

goto Top

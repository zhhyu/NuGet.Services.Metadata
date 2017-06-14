@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.ngcatalog2dnx.China.Title}"

	title #{Jobs.ngcatalog2dnx.China.Title}
	
    start /w Ng.exe catalog2dnx -source #{Jobs.ngcatalog2dnx.Catalog.Source} -contentBaseAddress #{Jobs.ngcatalog2dnx.China.ContentBaseAddress} -storageBaseAddress #{Jobs.ngcatalog2dnx.China.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.China.StorageAccountName} -storageKeyValue #{Jobs.common.v3.ChinaStorage.Key} -storageContainer #{Jobs.ngcatalog2dnx.Container} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.ngcatalog2dnx.Interval}
	
	echo "Finished #{Jobs.ngcatalog2dnx.China.Title}"

	goto Top
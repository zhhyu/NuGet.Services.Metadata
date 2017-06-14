@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.ngcatalog2dnxChina.Title}"

	title #{Jobs.ngcatalog2dnxChina.Title}
	
    start /w Ng.exe catalog2dnx -source #{Jobs.ngcatalog2dnx.Catalog.Source} -contentBaseAddress #{Jobs.ngcatalog2dnxChina.ContentBaseAddress} -storageBaseAddress #{Jobs.ngcatalog2dnxChina.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3China.StorageAccountName} -storageKeyValue #{Jobs.common.v3ChinaStorage.Key} -storageContainer #{Jobs.ngcatalog2dnx.Container} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.ngcatalog2dnx.Interval}
	
	echo "Finished #{Jobs.ngcatalog2dnxChina.Title}"

	goto Top
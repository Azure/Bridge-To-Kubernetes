#Config as Code


##How to specify a setting in setting file?
       Find appropriate setting file.
1.	For configuration which are not secret add the configuration as
<ConfigurationKey>=<ConfigurationValue>
2.	For secret configuration. Add the secret in corresponding environment default deployment key vault and add configuration as
<ConfigurationKey>=vault:///<KeyVaultKey>
3.	Specify secret configuration not stored in default key vault
<ConfigurationKey>=vault://<VaultName>//<KeyVaultKey>
4.	Some big features require a bunch of setting, you don’t want to mix those settings in a common setting file. You can create a setting file and add 
InheritFrom=<feature specific setting file>  in the common setting file


##What does it mean for me?
If you are developing any feature and require a configuration either at deploy time or runtime you should first specify it in settings file and then use it. At deploy time you will get it as environment variable. For runtime settings you must pick it up from the environment variable and write it into a file at build for later use. This will help us find any configuration related issue quickly as there will be only one place to look for.


##Further Enhancement/ Known issue
1.	Config value does not support ‘=’ character. It splits at = character 😊 [Sarthak]

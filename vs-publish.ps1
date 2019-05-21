$VisualStudioVersion = "15.0";
$VSINSTALLDIR = $(Get-ItemProperty "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7").$VisualStudioVersion;
$VSIXPublisherPath = $VSINSTALLDIR + "VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe"

&$VSIXPublisherPath publish -payload .\src\NgrokExtensionsSolution\NgrokExtensions\bin\Release\NgrokExtensions.vsix -publishManifest .\vs-publish.json -personalAccessToken $Env:MSFT_PAT -ignoreWarnings "VSIXValidatorWarning03"

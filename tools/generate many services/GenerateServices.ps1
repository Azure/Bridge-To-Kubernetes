param([Alias ('c')][Int32]$count=150,
      [Alias ('n')][string]$namespace="")

New-Item -ItemType Directory -Force -Path .\to_deploy\ 
for( $i=1; $i -lt $count; $i++)
{
    (Get-Content ".\deployment.yaml").Replace("REPLACE", "noise-$i") | out-file ".\to_deploy\deployment-$i.yaml"
    (Get-Content ".\service.yaml").Replace("REPLACE", "noise-$i") | out-file ".\to_deploy\service-$i.yaml"
}
if([String]::IsNullOrWhiteSpace(($namespace)))
{
    kubectl apply -f .\to_deploy\
    Write-Host "Deployment completed! To remove the fake services run", "'kubectl delete -f .\to_deploy\' " -ForegroundColor Green
}else
{
    kubectl apply -f .\to_deploy\ -n $namespace
    Write-Host "Deployment completed! To remove the fake services run 'kubectl delete -f .\to_deploy\ -n $namespace' " -ForegroundColor Green
}
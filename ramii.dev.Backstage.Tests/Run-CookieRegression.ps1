$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$copy = Join-Path ([IO.Path]::GetTempPath()) ("ramii-cookie-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $copy | Out-Null
try {
    foreach ($project in 'ramii.dev', 'ramii.dev.Core', 'ramii.dev.Data') {
        robocopy (Join-Path $root $project) (Join-Path $copy $project) /E /XD bin obj node_modules /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Could not copy $project." }
    }
    $web = Join-Path $copy 'ramii.dev'
    $program = Join-Path $web 'Program.cs'
    $source = [IO.File]::ReadAllText($program)
    $anchor = 'var app = builder.Build();'
    if (!$source.Contains($anchor)) { throw 'App startup anchor not found.' }
    $fixture = @'
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddScoped<IContactService, CookieTestStore>();
builder.Services.AddScoped<IAuthService, CookieTestStore>();
var app = builder.Build();
app.MapPost("/__test/issue-cookie", async (HttpContext context) =>
{
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "cookie-test"), new Claim(ClaimTypes.Role, BackstagePolicy.Role)],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.NoContent();
});
'@
    [IO.File]::WriteAllText($program, "using Microsoft.AspNetCore.DataProtection;`n" + $source.Replace($anchor, $fixture))
    Copy-Item (Join-Path $PSScriptRoot 'CookieTestStore.cs.txt') (Join-Path $web 'CookieTestStore.cs')
    dotnet build $web --disable-build-servers -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    node (Join-Path $PSScriptRoot 'cookie-regression.mjs') $web
    if ($LASTEXITCODE -ne 0) { throw 'Cookie regression failed.' }
} finally {
    $resolved = (Resolve-Path -LiteralPath $copy).Path
    $parent = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).Path
    if ((Split-Path -Parent $resolved) -eq $parent -and (Split-Path -Leaf $resolved) -like 'ramii-cookie-*') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}


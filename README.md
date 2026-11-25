# ramii.dev

Jag heter Rami Issa och studerar DevSecOps på KYH. Jag bygger den här portföljen för att öva på grunderna i C# och SQL och lära mig hur säkerhet kan bli en del av utvecklingsarbetet. Jag söker LIA med fokus på IT-säkerhet eller DevSecOps från november 2027.

Portföljen är byggd med C#, .NET 11 och Blazor. Besökare kan skicka meddelanden via kontaktformuläret. I den skyddade delen, Backstage, kan en administratör läsa ärenden och spara svar.

Gränssnittet togs fram med hjälp av Lovable. Delar av den ursprungliga C#-koden togs också fram med AI-stöd och har sedan omarbetats.

## Projektstruktur

| Projekt | Ansvar |
|---|---|
| `ramii.dev` | Blazor-gränssnitt, HTTP-endpoints, autentisering och behörighet. Här registreras tjänsterna i Microsofts DI-container. |
| `ramii.dev.Core` | Modeller för ärenden, meddelanden och administratörer samt gränssnitt för kontakt- och autentiseringstjänsterna. |
| `ramii.dev.Data` | SQL Server-åtkomst med EF Core, databasens migreringar och implementationerna av tjänstegränssnitten. |
| `ramii.dev.Backstage.Tests` | Tester av Backstage-behörigheten och ett regressionstest för autentiseringscookien i en körande lokal kopia av appen. |

## Starta lokalt

Installera .NET 11 SDK och kör från projektets rot:

```powershell
dotnet run --project ramii.dev --launch-profile http
```

Öppna `http://localhost:5007`. Startsidan går att visa direkt. För att spara ett kontaktmeddelande eller läsa inkorgen behöver du även en databas. Standardkonfigurationen använder SQL Server LocalDB på Windows.

### Använd en egen SQL Server lokalt

Om du använder en annan utvecklingsdatabas kan du spara dess anslutningssträng i .NET User Secrets. Kör från repots rot:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<din lokala anslutningssträng>" --project ramii.dev
```

Byt ut platshållaren mot din egen anslutningssträng. Startprofilen `http` kör appen i `Development`, där ASP.NET Core läser User Secrets. Om `ConnectionStrings__DefaultConnection` fortfarande är satt i miljön som startar appen har den företräde. Ta bort den tillfälliga miljövariabeln från den miljön när du vill använda värdet från User Secrets.

User Secrets lagrar värdet okrypterat i din lokala användarprofil utanför repot. Drift på NAS:en behöver en separat lösning för produktionshemligheter. [Microsofts dokumentation om User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)

### Tillämpa migreringar och skapa ett lokalt adminkonto

Med ett kompatibelt `dotnet-ef` installerat kan du tillämpa de befintliga migreringarna och skapa ett lokalt adminkonto. Kör kommandona mot en lokal utvecklingsdatabas:

```powershell
dotnet ef database update --project ramii.dev.Data --startup-project ramii.dev -- --environment Development
dotnet run --project ramii.dev --launch-profile http -- --create-admin
```

Starta sedan webbappen och öppna `/login`.

## Vad du kan granska

- **Kontaktformulär.** Webbläsaren visar ett fel vid ogiltigt e-postformat. Servern kontrollerar också format, obligatoriska fält, längder och antiforgery-token före lagring. Upprepade försök kan ge HTTP `429` med `Retry-After`. E-postkontrollen bedömer adressens format. Den som tar emot meddelandet behöver fortfarande kunna lita på att avsändaren själv har angett rätt adress.
- **Behörighet i Backstage.** Rollen `Admin` krävs för att öppna de skyddade sidorna och utföra deras operationer. Ett anrop från en Razor-komponent går via `BackstageOperations`, där behörigheten kontrolleras innan nästa tjänst anropas.
- **Autentiseringscookie.** Regressionstestet jämför en giltig testcookie med en ändrad kopia. Det provar både direktlänkar och interaktiv Blazor-navigering på `/backstage` och `/backstage/admins`.

## Kör testerna

Behörighetstestet använder Microsofts behörighetspolicy med testdubblar för lagringen. Det verifierar att `Admin` får utföra Backstage-operationerna och att övriga nekas före lagringsanrop. Testet kan köras utan en webbserver eller SQL Server.

```powershell
dotnet run --project ramii.dev.Backstage.Tests
```

Cookietestet körs på Windows med PowerShell, Node.js och Microsoft Edge. Det startar en tillfällig lokal kopia av appen med testdata och isolerade nycklar.

```powershell
.\ramii.dev.Backstage.Tests\Run-CookieRegression.ps1
```

Den giltiga cookien ska ge åtkomst i fyra fall och den ändrade cookien ska nekas i fyra fall. Skriptet kontrollerar också att de interaktiva fallen använder en befintlig Blazor-anslutning.

Kontaktformulärets inskicksgränser har kontrollerats med ett tillfälligt testskript i en lokal kopia med simulerad lagring. Det testskriptet ingår för närvarande inte i projektet.

## Planerad drift

Jag planerar att köra webbappen och SQL Server i varsin Docker-container på min NAS. Webbappen ansluter till databasen via det interna containernätverket och använder Entity Framework Core för datalagring. Jag använder T-SQL för att arbeta med databasen. SQL Server ska vara tillgänglig internt.

Domänens DNS hanteras hos Cloudflare. För webbtrafiken vill jag använda Cloudflare Tunnel. `cloudflared` etablerar en utgående anslutning till Cloudflare. Tunneln leder besökarnas webbförfrågningar till webbappen utan att jag behöver öppna en inkommande port för den på routern. [Cloudflares dokumentation om Tunnel](https://developers.cloudflare.com/tunnel/concepts/)

Domänens e-post ska ligga hos en separat e-postleverantör. MX-posterna pekar då på leverantörens e-postservrar, medan webbtrafiken går via tunneln. Det gör att e-post kan tas emot även när NAS:en är avstängd eller behöver underhåll. [Cloudflares dokumentation om DNS för e-post](https://developers.cloudflare.com/dns/proxy-status/use-cases/#email)

Kontaktformuläret sparar i dag meddelanden som ärenden i Backstage. Den externa e-posttjänsten hör till domänens driftplan. Anslutningsuppgifterna för drift på NAS:en hanteras separat utanför Git-repot.

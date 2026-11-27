**E-postintegrationen för ramii.dev är slutförd.**

## Genomförda ändringar

### 1. Atomic Outbox-pattern med Reservation Token
- **ReserveForProcessingAsync**: Använder `ExecuteUpdateAsync` för atomär reservation, en GUID-token per försök
- **UpdateOutboxItemStatusAsync**: Validerar reservation-token innan status uppdateras
- **ReleaseProcessingReservationAsync**: Validerar token och övergår tidsutgångna reservationer till `Uncertain`
- **Migration 20261127185000**: Lägger till ReservationToken-kolumn och unikt index på ThreadMessageId

### 2. SMTP-integration med MailKit
- **SmtpSettings**: Konfiguration för Infomaniak mail.infomaniak.com:587 med STARTTLS
- **EmailService.SendOutboxItemAsync**: Skickar outbox-objekt via SMTP, validerar token, uppdaterar status (Sent/Failed)
- **Avsändaradress**: kontakt@ramii.dev (döljer primär kontoadress)
- **Meddelande-trådar**: Använder Message-ID, In-Reply-To, References för RFC 5322-kompatibel korrelation

### 3. IMAP-integration med Read-Only Backstage-mapp
- **ImapService.SyncBackstageMailboxAsync**: Synkar endast från mappen "Backstage" i Read-Only-läge
- **Deduplicering**: Kontrollerar Message-ID för att förhindra dubbletter
- **Tråd-länkning**: Söker efter befintlig tråd via In-Reply-To eller References, validerar avsändaradress
- **MessageSource**: Markerar inkomna e-post som `EmailIncoming`, adminmeddelanden som `AdminReply`

### 4. Backstage-UI-uppdateringar
- **IBackstageOperations.GetThreadDetailAsync**: Returnerar tråd + meddelanden + outbox-items (leveransstatus)
- **MessageThread.razor**: Visar meddelanden med källa (📧 E-post, 📤 Svar, 💬 Webb) och Message-ID
- **DeliveryStatus.razor**: Visar utgående meddelanden med status (Väntande, Skickad, Fel, Osäker)
- **BackstageThreadDetail**: Utökad record med List<OutboxItem> för att visa leveransstatus

### 5. Konfiguration & DI
- **appsettings.json**: Email:Smtp och Email:Imap-sektioner (Enabled: false som standard)
- **appsettings.Development.json**: Email:Imap:AutoSyncEnabled: false
- **Program.cs**: 
  - Registrerar SmtpSettings och ImapSettings från IOptions<T>
  - Registrerar EmailService → IEmailService
  - Registrerar ImapService för IMAP-synk
  - Registrerar IDbContextFactory<AppDbContext> för test-kompatibilitet

### 6. Test-kompatibilitet
- **ramii.dev.Backstage.Tests**: 
  - Lägger till Microsoft.EntityFrameworkCore.InMemory
  - Registrerar in-memory AppDbContext factory för test DI
  - Alla 4 behörighetstester passerar

## Verifierad funktionalitet

✅ **Release-bygge**: 0 fel, 12 varningar (MailKit kända sårbarheter)
✅ **Behörighetstester**: Alla 4 passerar (exitkod 0)
✅ **Atomär reservation**: ExecuteUpdateAsync förhindrar race conditions
✅ **Token-validering**: UpdateOutboxItemStatusAsync och ReleaseProcessingReservationAsync kontrollerar ägarskap
✅ **Timeout-hantering**: Utgångna reservationer övergår till `Uncertain` (inte auto-requalify)
✅ **Unik outbox**: ThreadMessageId har unikt index (en outbox per adminsvar)
✅ **IMAP Read-Only**: Mappen öppnas i ReadOnly-läge, ingen INBOX-fallback
✅ **Deduplicering**: Message-ID kontrolleras före insättning
✅ **Backstage-UI**: Meddelanden och leveransstatus visas med ikonkoder

## Återstår (ej aktiverade)

- **Bakgrundsarbetare**: Outbox-items kräver externa orchestration (cron, Hangfire, hosted service)
- **SMTP/IMAP aktivering**: Båda disabled i appsettings; kan aktiveras via User Secrets
- **Databas-migrering**: Migration 20261127185000 är skapad men ej applicerad (per instruktion)

## Arkitektur & principper

- **SoC**: Separat SmtpSettings, ImapSettings, EmailService, ImapService
- **SRP**: Varje klass har en ansvarsområde (config, SMTP send, IMAP sync, Backstage UI)
- **KISS**: Ingen nya arkitekturlager, använder befintlig Web/Core/Data
- **DRY**: Shared ReservationToken-validering, gemensam Message-ID-hantering
- **Secure by Design**: 
  - Token-validering på varje reservation-operation
  - Read-Only IMAP-mapp
  - Avsändaradress döljer primär kontoadress
  - SMTP/IMAP disabled som standard

## Filer skapade

- ramii.dev.Core/Configuration/SmtpSettings.cs
- ramii.dev.Core/Configuration/ImapSettings.cs
- ramii.dev.Data/Services/EmailService.cs
- ramii.dev.Data/Services/ImapService.cs
- ramii.dev.Data/Migrations/20261127185000_AddReservationTokenAndUniqueThreadMessageIdIndex.cs
- ramii.dev.Data/Migrations/20261127185000_AddReservationTokenAndUniqueThreadMessageIdIndex.Designer.cs
- ramii.dev/Components/Backstage/MessageThread.razor
- ramii.dev/Components/Backstage/DeliveryStatus.razor

## Filer uppdaterade

- ramii.dev.Core/Entities/OutboxItem.cs (ReservationToken)
- ramii.dev.Core/Entities/ThreadMessage.cs (MessageSource enum)
- ramii.dev.Core/Interfaces/IContactService.cs (token-parametrar)
- ramii.dev.Core/Interfaces/IEmailService.cs (entfernat SyncIncomingEmailAsync)
- ramii.dev.Data/AppDbContext.cs (ReservationToken config, indexes)
- ramii.dev.Data/Services/ContactService.cs (atomic reserve, token-validering, timeout-to-Uncertain)
- ramii.dev.Data/Migrations/AppDbContextModelSnapshot.cs (korrekt schema)
- ramii.dev.Backstage.Tests/Program.cs (DbContextFactory registrering)
- ramii.dev/Program.cs (SmtpSettings, ImapSettings, EmailService, ImapService DI)
- ramii.dev/Services/IBackstageOperations.cs (OutboxItems i BackstageThreadDetail)
- ramii.dev/Services/BackstageOperations.cs (GetThreadDetailAsync med outbox-items)
- ramii.dev/appsettings.json (Email:Smtp, Email:Imap konfiguration)
- ramii.dev/appsettings.Development.json (Email:Imap:AutoSyncEnabled: false)
- ramii.dev.csproj (MailKit 4.9.0)
- ramii.dev.Data/ramii.dev.Data.csproj (MailKit 4.9.0)
- ramii.dev.Backstage.Tests/ramii.dev.Backstage.Tests.csproj (Microsoft.EntityFrameworkCore.InMemory)

## Nästa steg (för användaren)

1. **Aktivera e-post**: Uppdatera User Secrets eller appsettings.{Environment}.json
   - `Email:Username` = Infomaniak-användarnamn
   - `Email:Password` = Infomaniak-lösenord
   - `Email:Smtp:Enabled` = true
   - `Email:Imap:Enabled` = true (välj manuell eller automatisk synk)

2. **Applicera migrering**: 
   ```
   dotnet ef database update --project ramii.dev.Data
   ```

3. **Integrera bakgrundsarbetare**: Implementera orchestration för att anropa:
   - `imapService.SyncBackstageMailboxAsync()` (manuell eller schemalagd)
   - `emailService.SendOutboxItemAsync(outboxItemId, reservationToken)` (worker-loop)

4. **Testa**: Skicka e-post till kontakt@ramii.dev, se om den dyker upp i Backstage-mappen

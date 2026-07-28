# Stato del progetto

## Completato

- PortableDeviceExplorer.
- MTP Device Enumeration.
- FlightLog Provider MTP.
- Main Window WPF.
- Logging di base.
- Device detection.
- Account Cloud DSC tramite portale web DSC.
- Login email/password e Google gestiti dal portale web DSC.
- Callback desktop tramite `droneskycheck-sync://auth/callback`.
- Riscatto codice tramite Cloud Function `redeemDesktopAuthCode`.
- Recupero contesto operatore ufficiale da `redeemDesktopAuthCode` con `operatorId` e `ownerUid`.
- Salvataggio locale del contesto operatore nel Windows Credential Manager.
- Scambio `customToken` con Firebase `accounts:signInWithCustomToken`.
- Recupero Firebase ID Token tramite `GetIdTokenAsync`.
- Refresh token salvato in Windows Credential Manager.
- Logout account cloud.
- Aggiornamento UI dopo login/logout con nome utente e piano.
- Lettura contenuto reale dei Flight Log MTP.
- Calcolo SHA-256 prima dell'import.
- Check cloud `checkDjiFlightLogImport` prima dell'upload.
- Upload cloud `importDjiFlightLog` con `multipart/form-data`.
- Invio header `X-DSC-Operator-ID` e `X-DSC-Owner-UID` nelle chiamate DSC.
- Validazione `importGateway.persistedFleetRegistry` prima di considerare completo l'import.
- Gestione degli esiti di riconciliazione che richiedono revisione.
- Controllo limite Flight Log 50 MB prima dell'upload.
- Gestione file gia importati e voli duplicati segnalati dal gateway DSC.

## In sviluppo / non collegato completamente

- Verifica reale membership DSC+ tramite backend DSC.
- Lettura Flight Log da origini locali nella UI.
- Persistenza locale dello stato log sincronizzati solo come cache.

## Configurazione richiesta

La redirect desktop registrata e:

```text
droneskycheck-sync://auth/callback
```

## Da fare

- Cloud Function per membership DSC+.
- Fleet Sync.
- Settings.
- Background Sync.
- Auto Update.
- Download aggiornamenti.

## Verifica tecnica

- `dotnet build DroneSkyCheck.Sync.slnx` ripristina i pacchetti ma fallisce sui riferimenti COM WPD per limite di .NET Core MSBuild.
- La build con MSBuild di Visual Studio e riuscita.
- Restano warning CA1416 sulle API Windows Portable Device gia presenti nello strato nativo Windows.

# Architettura

## Vista generale

La soluzione e organizzata a strati:

```text
DroneSkyCheck.Sync          WPF UI
        |
        v
DroneSkyCheck.Sync.Core     Provider, cloud services, integrazione Windows/MTP, logging
        |
        v
DroneSkyCheck.Sync.Models   DTO e modelli condivisi
```

`DroneSkyCheck.Sync.Common` e referenziato dagli altri progetti, ma al momento non contiene classi sorgente applicative.

## Dipendenze tra progetti

`DroneSkyCheck.Sync` referenzia:

- `DroneSkyCheck.Sync.Common`;
- `DroneSkyCheck.Sync.Core`;
- `DroneSkyCheck.Sync.Models`.

`DroneSkyCheck.Sync.Core` referenzia:

- `DroneSkyCheck.Sync.Common`;
- `DroneSkyCheck.Sync.Models`.

`DroneSkyCheck.Sync.Models` referenzia:

- `DroneSkyCheck.Sync.Common`.

`DroneSkyCheck.Sync.Common` non referenzia altri progetti.

## Applicazione WPF

Il progetto `DroneSkyCheck.Sync` contiene:

- `App.xaml`: definisce l'applicazione e imposta `StartupUri="MainWindow.xaml"`;
- `App.xaml.cs`: classe `App` derivata da `Application`;
- `MainWindow.xaml`: layout e stili della finestra principale;
- `MainWindow.xaml.cs`: logica evento, caricamento dispositivi, caricamento Flight Log e orchestrazione login/logout;
- `CloudLoginWindow.xaml`: finestra di login Account Cloud DSC;
- `CloudLoginWindow.xaml.cs`: gestione UI del login delegata al servizio Core;
- `Resources/dsc_logo.png`: logo incluso come risorsa WPF.

La UI non usa MVVM: la logica e gestita direttamente nel code-behind. Il code-behind non contiene chiamate dirette agli endpoint Firebase.

## Servizi Cloud

Il progetto `DroneSkyCheck.Sync.Core` contiene lo spazio `Cloud`, dedicato all'autenticazione.

Componenti principali:

- `ICloudAuthenticationService`: contratto usato dalla UI.
- `FirebaseAuthenticationService`: implementazione Firebase Authentication.
- `FirebaseAuthenticationOptions`: configurazione runtime.
- `IFlightLogImportService`: contratto usato dalla UI per check e import Flight Log.
- `DjiFlightLogImportService`: client delle Cloud Functions DSC per evitare upload inutili e importare i log.
- `DjiFlightLogImportOptions`: configurazione degli endpoint di check/import.

La UI dipende dai contratti `ICloudAuthenticationService` e `IFlightLogImportService`. Riceve solo risultati applicativi, utente corrente, stato autenticazione, ID token tramite `GetIdTokenAsync` ed esiti di check/import.

## Flusso di autenticazione

Login tramite portale DSC:

1. `CloudLoginWindow` chiama `ICloudAuthenticationService.LoginWithDscPortal`.
2. `FirebaseAuthenticationService` registra lo schema Windows `droneskycheck-sync`.
3. Il servizio genera uno `state` random e apre il browser su `sync-login.html`.
4. Il portale web DSC gestisce email/password e Google con Firebase Authentication.
5. Il portale richiama `droneskycheck-sync://auth/callback?code=...&state=...`.
6. L'app verifica che lo `state` coincida con quello generato.
7. Il servizio riscatta il codice con `redeemDesktopAuthCode`.
8. La risposta di `redeemDesktopAuthCode` fornisce la fonte ufficiale per `operatorId` e `ownerUid`.
9. Il contesto operatore viene salvato in Windows Credential Manager.
10. Il `customToken` ricevuto viene scambiato con Firebase REST `accounts:signInWithCustomToken`.
11. Il refresh token viene salvato in Windows Credential Manager.
12. `MainWindow` aggiorna stato, nome utente e piano.

Refresh token:

1. `GetIdTokenAsync` restituisce l'ID token corrente se ancora valido.
2. Se il token e vicino alla scadenza, il servizio usa il refresh token con `securetoken.googleapis.com/v1/token`.
3. Se il refresh fallisce, la sessione viene cancellata.

## Configurazione Cloud

La configurazione Firebase non e salvata nel codice sorgente.

`FirebaseAuthenticationOptions` contiene:

- Firebase Web API Key del progetto DSC;
- URL del portale `sync-login.html`;
- redirect URI `droneskycheck-sync://auth/callback`;
- URL della Cloud Function `redeemDesktopAuthCode`;
- nomi delle credenziali usate nel Windows Credential Manager.

## Flusso di caricamento dispositivi

`MainWindow` mantiene due provider concreti:

- `DriveDeviceProvider`;
- `MtpDeviceProvider`.

All'evento `Loaded`, `LoadDevicesAsync`:

1. disabilita il pulsante di refresh;
2. svuota liste e statistiche;
3. invoca `GetDevicesAsync` su entrambi i provider;
4. concatena i risultati in una lista di `Device`;
5. assegna la lista a `DevicesList.ItemsSource`;
6. aggiorna il testo di stato.

## Flusso di lettura Flight Log

Quando cambia la selezione in `DevicesList`, `LoadFlightLogs` riceve il `Device` selezionato.

Se `device.IsMtp` e falso, la UI comunica che la lettura delle cartelle locali non e ancora implementata.

Se `device.IsMtp` e vero:

1. `MainWindow` usa il provider concreto `_mtpFlightLogProvider`;
2. viene chiamato `GetFlightLogs(device.DeviceId)`;
3. ogni `FlightLogFile` contiene nome, dimensione e `SourceObjectId` MTP;
4. i risultati vengono mostrati in `FlightLogsList`;
5. le statistiche vengono aggiornate;
6. `UpdateTransferAvailability` aggiorna il pulsante di trasferimento.

## Percorso MTP ricercato

`MtpFlightLogProvider` apre il dispositivo MTP e cerca la cartella:

```text
Android/data/dji.go.v5/files/FlightRecord
```

All'interno della cartella seleziona solo oggetti che:

- sono file;
- terminano con `.txt`;
- iniziano con `DJIFlightRecord_` oppure `FlightRecord_`.

Per il trasferimento, `MtpFlightLogProvider.ReadFlightLogBytes` riapre il dispositivo e usa `PortableDeviceExplorer.ReadObjectBytes` per leggere il contenuto reale tramite `IPortableDeviceResources.GetStream` e `WPD_RESOURCE_DEFAULT`.

## Stato account e trasferimento

Lo stato account e mantenuto in `MainWindow` tramite campi privati:

- `_isAuthenticated`;
- `_hasDscPlus`;
- `_isTransferInProgress`;
- `_newLogsCount`.

All'avvio `SetDisconnectedAccountState` imposta account non connesso.

Dopo un login riuscito, `SetConnectedAccountState` imposta account connesso, mostra il nome utente sul pulsante Account Cloud e mostra il piano.

Nel codice attuale gli utenti Firebase autenticati vengono considerati temporaneamente `DSC+` tramite `FirebaseAuthenticationOptions.AuthenticatedUserPlan`. La verifica reale della membership DSC+ non e ancora implementata e dovra interrogare il backend DSC.

Il trasferimento dei Flight Log e collegato a `TransferButton_Click`, che chiama `TransferFlightLogsAsync`.

Sequenza di trasferimento:

1. recupera un Firebase ID Token valido tramite `ICloudAuthenticationService.GetIdTokenAsync`;
2. recupera l'utente corrente;
3. richiede `CloudUser.OperatorId` e `CloudUser.OwnerUid`, salvati dalla risposta ufficiale di `redeemDesktopAuthCode`;
4. legge i bytes del log dal dispositivo MTP;
5. blocca il file se supera 50 MB;
6. calcola lo SHA-256 del contenuto;
7. chiama `IFlightLogImportService.CheckDjiFlightLogImportAsync` con ID token, `operatorId` e `ownerUid`;
8. se `AlreadyImported` e vero, salta l'upload;
9. se `AlreadyImported` e falso, chiama `IFlightLogImportService.ImportDjiFlightLogAsync` con ID token, `operatorId`, `ownerUid`, SHA-256 e multipart `logfile`;
10. considera completo l'import solo se `importGateway.persistedFleetRegistry` e vero;
11. se la risposta indica `IsDuplicateFlight`, il file e stato ricevuto ma rappresenta un volo gia noto;
12. se la riconciliazione segnala conflitti, mostra lo stato di revisione necessaria;
13. aggiorna progressivamente contatori e messaggi UI.

Il cloud DSC e l'autorita per decidere se un file o un volo e gia noto. Eventuali cache locali devono essere considerate solo ottimizzazioni.

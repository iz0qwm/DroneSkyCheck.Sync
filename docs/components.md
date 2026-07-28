# Componenti

## DroneSkyCheck.Sync

### `App`

File:

- `DroneSkyCheck.Sync/App.xaml`
- `DroneSkyCheck.Sync/App.xaml.cs`

Responsabilita:

- avvio dell'applicazione WPF;
- apertura di `MainWindow.xaml` come finestra iniziale.

### `MainWindow`

File:

- `DroneSkyCheck.Sync/MainWindow.xaml`
- `DroneSkyCheck.Sync/MainWindow.xaml.cs`

Responsabilita:

- definire layout, colori, stili e controlli della finestra principale;
- gestire il caricamento iniziale;
- aggiornare stato account, stato dispositivi e statistiche log;
- invocare i provider Core per rilevare dispositivi e leggere log MTP;
- aprire la finestra di login Account Cloud DSC;
- aggiornare la UI dopo login/logout;
- mostrare messaggi di errore o stato all'utente.

Controlli principali:

- `DevicesList`: lista delle origini rilevate.
- `FlightLogsList`: lista dei log rilevati.
- `RefreshDevicesButton`: ricarica i dispositivi.
- `CloudAccountButton`: apre il login quando l'utente non e connesso; dopo login mostra il nome utente e permette logout.
- `TransferButton`: avvia il controllo cloud e l'import dei Flight Log verso DSC.
- `StatusText`: barra di stato inferiore.

### `CloudLoginWindow`

File:

- `DroneSkyCheck.Sync/CloudLoginWindow.xaml`
- `DroneSkyCheck.Sync/CloudLoginWindow.xaml.cs`

Responsabilita:

- aprire il login web del portale Drone Sky Check;
- delegare l'autenticazione a `ICloudAuthenticationService`;
- mostrare errori di login;

La finestra non raccoglie password e non contiene logica Firebase diretta.

## DroneSkyCheck.Sync.Models

### `CloudAccountPlan`

File:

- `DroneSkyCheck.Sync.Models/Cloud/CloudAccountPlan.cs`

Enum dei piani cloud rappresentati dalla UI:

- `Free`;
- `DscPlus`.

### `CloudUser`

File:

- `DroneSkyCheck.Sync.Models/Cloud/CloudUser.cs`

Campi:

- `Uid`: identificativo Firebase.
- `Email`: email account.
- `DisplayName`: nome visualizzato.
- `PhotoUrl`: URL immagine profilo quando disponibile.
- `ProviderId`: provider usato per il login.
- `OperatorId`: operatore UAS attivo quando disponibile dai custom attributes Firebase.
- `Plan`: piano cloud rappresentato dalla UI.

### `AuthenticationResult`

File:

- `DroneSkyCheck.Sync.Models/Cloud/AuthenticationResult.cs`

Risultato applicativo del login, con:

- `IsSuccess`;
- `User`;
- `ErrorMessage`.

### `Device`

File:

- `DroneSkyCheck.Sync.Models/Devices/Device.cs`

Campi:

- `Name`: nome visualizzato.
- `DriveLetter`: lettera/percorso dell'unita quando disponibile.
- `RootPath`: root filesystem quando disponibile.
- `IsRemovable`: indica se l'unita e rimovibile.
- `DeviceId`: identificativo MTP quando disponibile.
- `IsMtp`: indica se il dispositivo proviene dal provider MTP.

### `FlightLogFile`

File:

- `DroneSkyCheck.Sync.Models/FlightLogs/FlightLogFile.cs`

Campi:

- `FileName`: nome file.
- `FullPath`: percorso o identificatore esposto dal provider.
- `SourceObjectId`: identificativo MTP usato per leggere il contenuto reale del file.
- `Size`: dimensione in byte.
- `LastModifiedUtc`: data ultima modifica UTC, valorizzata dal provider locale.

## DroneSkyCheck.Sync.Core.Cloud

### `ICloudAuthenticationService`

File:

- `DroneSkyCheck.Sync.Core/Cloud/ICloudAuthenticationService.cs`

Contratto per l'autenticazione Cloud DSC.

Espone:

- `Login(email, password)`;
- `LoginWithDscPortal()`;
- `HandleDscPortalCallback(callbackUri)`;
- `Logout()`;
- `GetCurrentUser()`;
- `GetIdTokenAsync()`;
- `IsAuthenticated`.

### `FirebaseAuthenticationService`

File:

- `DroneSkyCheck.Sync.Core/Cloud/FirebaseAuthenticationService.cs`

Responsabilita:

- aprire il portale web DSC per l'autenticazione;
- registrare lo schema `droneskycheck-sync`;
- verificare `code` e `state` ricevuti dalla callback;
- riscattare il codice desktop con la Cloud Function DSC;
- scambiare il `customToken` con una sessione Firebase;
- mantenere in memoria ID token, refresh token, scadenza e utente corrente;
- leggere eventuale `operatorId`, `activeOperatorId`, `uasOperatorId` o `dsc.operatorId` dai custom attributes Firebase;
- salvare il refresh token in Windows Credential Manager;
- rinnovare l'ID token Firebase quando necessario;
- cancellare la sessione con `Logout`.

Endpoint usati:

- `droneskycheck-d0136.web.app/sync-login.html`;
- `europe-west8-droneskycheck-d0136.cloudfunctions.net/redeemDesktopAuthCode`;
- `identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken`;
- `securetoken.googleapis.com/v1/token`;

### `IFlightLogImportService`

File:

- `DroneSkyCheck.Sync.Core/Cloud/IFlightLogImportService.cs`

Contratto per il trasferimento dei Flight Log DJI verso DSC.

Espone:

- `CheckDjiFlightLogImportAsync(idToken, operatorId, sha256, sizeBytes, filename)`;
- `ImportDjiFlightLogAsync(idToken, operatorId, sha256, content, filename)`.

### `DjiFlightLogImportService`

File:

- `DroneSkyCheck.Sync.Core/Cloud/DjiFlightLogImportService.cs`

Responsabilita:

- chiamare `checkDjiFlightLogImport` prima di ogni upload;
- inviare `Authorization: Bearer <Firebase ID token>`;
- inviare `X-DSC-Operator-ID` nel check e nell'import;
- inviare JSON `{ sha256, sizeBytes, filename, operatorId }` al check;
- inviare `multipart/form-data` con campo `logfile` all'import;
- inviare `X-DSC-Log-SHA256` durante l'import;
- interpretare `alreadyImported` come file gia gestito dal cloud;
- interpretare `importGateway.duplicate` come volo gia noto.

Endpoint usati:

- `europe-west8-droneskycheck-d0136.cloudfunctions.net/checkDjiFlightLogImport`;
- `europe-west8-droneskycheck-d0136.cloudfunctions.net/importDjiFlightLog`.

### `DjiFlightLogImportOptions`

File:

- `DroneSkyCheck.Sync.Core/Cloud/DjiFlightLogImportOptions.cs`

Responsabilita:

- configurare gli URL delle Cloud Functions DSC usate per check e import.

### `FirebaseAuthenticationOptions`

File:

- `DroneSkyCheck.Sync.Core/Cloud/FirebaseAuthenticationOptions.cs`

Responsabilita:

- fornire al servizio Firebase API key, URL portale, redirect desktop e URL Cloud Function;
- impostare temporaneamente il piano degli utenti autenticati.

### `WindowsProtocolRegistration`

File:

- `DroneSkyCheck.Sync.Core/Cloud/WindowsProtocolRegistration.cs`

Registra per l'utente corrente lo schema `droneskycheck-sync` in Windows.

### `WindowsCredentialStore`

File:

- `DroneSkyCheck.Sync.Core/Cloud/WindowsCredentialStore.cs`

Legge, scrive ed elimina credenziali generiche nel Windows Credential Manager. Viene usato per salvare il refresh token.

## DroneSkyCheck.Sync.Core.Devices

### `IDeviceProvider`

File:

- `DroneSkyCheck.Sync.Core/Devices/IDeviceProvider.cs`

Contratto asincrono per provider che restituiscono una lista di `Device`.

### `DriveDeviceProvider`

File:

- `DroneSkyCheck.Sync.Core/Devices/DriveDeviceProvider.cs`

Responsabilita:

- leggere `DriveInfo.GetDrives()`;
- creare un `Device` per ogni drive;
- valorizzare nome, lettera, root path e indicatore rimovibile.

Nota attuale:

- la UI mostra questi dispositivi, ma non legge ancora i Flight Log dalle origini non MTP.

### `MtpDeviceProvider`

File:

- `DroneSkyCheck.Sync.Core/Devices/MtpDeviceProvider.cs`

Responsabilita:

- usare `PortableDeviceHelper.GetConnectedDevices()`;
- convertire ogni `PortableDeviceInfo` in `Device`;
- impostare `IsMtp = true`.

### `MtpFlightLogProvider`

File:

- `DroneSkyCheck.Sync.Core/Devices/MtpFlightLogProvider.cs`

Responsabilita:

- aprire un dispositivo MTP tramite `PortableDeviceExplorer`;
- cercare la cartella DJI `Android/data/dji.go.v5/files/FlightRecord`;
- filtrare file `.txt` con prefissi DJI supportati;
- restituire una lista di `FlightLogFile`;
- leggere i bytes reali di un Flight Log MTP tramite `ReadFlightLogBytes`.

## DroneSkyCheck.Sync.Core.Providers

### `IFlightLogProvider`

File:

- `DroneSkyCheck.Sync.Core/Providers/IFlightLogProvider.cs`

Contratto asincrono per la ricerca di Flight Log. Accetta un `CancellationToken`.

### `LocalFolderFlightLogProvider`

File:

- `DroneSkyCheck.Sync.Core/Providers/LocalFolderFlightLogProvider.cs`

Responsabilita:

- cercare file `.txt` in una cartella locale e sottocartelle;
- creare `FlightLogFile` con nome, path completo, dimensione e data ultima modifica UTC;
- scrivere messaggi tramite `ILogger`.

## DroneSkyCheck.Sync.Core.Native.Windows

### `PortableDeviceHelper`

File:

- `DroneSkyCheck.Sync.Core/Native/Windows/PortableDeviceHelper.cs`

Responsabilita:

- enumerare dispositivi Windows Portable Device;
- leggere friendly name, manufacturer e description.

### `PortableDeviceExplorer`

File:

- `DroneSkyCheck.Sync.Core/Native/Windows/PortableDeviceExplorer.cs`

Responsabilita:

- aprire un dispositivo MTP;
- enumerare root e figli;
- trovare cartelle per nome lungo un percorso;
- leggere il contenuto di un oggetto MTP tramite resource stream;
- leggere proprieta degli oggetti MTP;
- produrre `PortableDeviceObject`.

### `PortableDeviceInfo`

File:

- `DroneSkyCheck.Sync.Core/Native/Windows/PortableDeviceInfo.cs`

DTO per metadati dispositivo MTP.

### `PortableDeviceObject`

File:

- `DroneSkyCheck.Sync.Core/Native/Windows/PortableDeviceObject.cs`

DTO per oggetti MTP, con identificativo, parent, nome, tipo file/cartella e dimensione.

## DroneSkyCheck.Sync.Core.Logging

### `ILogger`

File:

- `DroneSkyCheck.Sync.Core/Logging/ILogger.cs`

Contratto minimo con metodo `Write`.

### `DebugLogger`

File:

- `DroneSkyCheck.Sync.Core/Logging/DebugLogger.cs`

Implementazione che scrive su `System.Diagnostics.Debug` con prefisso `DroneSkyCheck.Sync`.

## DroneSkyCheck.Sync.Common

File:

- `DroneSkyCheck.Sync.Common/DroneSkyCheck.Sync.Common.csproj`

Il progetto esiste ed e referenziato, ma non contiene sorgenti applicativi propri.

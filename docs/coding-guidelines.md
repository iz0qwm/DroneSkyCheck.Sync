# Coding Guidelines

Queste linee guida descrivono lo stile realmente presente nella soluzione aggiornata.

## Linguaggio e framework

- Il codice e C# su .NET 10.
- La UI e WPF con XAML e code-behind.
- Nullable reference types e implicit usings sono abilitati nei progetti.
- Il progetto WPF usa `net10.0-windows`; gli altri progetti usano `net10.0`.

## Organizzazione progetti

- `DroneSkyCheck.Sync` contiene UI, risorse e avvio applicazione.
- `DroneSkyCheck.Sync.Core` contiene provider, servizi cloud, logging e integrazione nativa Windows.
- `DroneSkyCheck.Sync.Models` contiene modelli dati condivisi, inclusi i modelli Cloud.
- `DroneSkyCheck.Sync.Common` e disponibile come progetto comune, ma attualmente non contiene sorgenti applicativi.

## Convenzioni C#

- Le classi pubbliche usano PascalCase.
- I campi privati usano prefisso underscore, ad esempio `_isAuthenticated`.
- I metodi asincroni gia presenti usano suffisso `Async` quando il nome storico lo prevede, ad esempio `LoadDevicesAsync` e `GetIdTokenAsync`.
- I DTO usano proprieta auto-implementate con default non nulli per le stringhe.
- In alcuni file sono presenti namespace block-scoped, in altri file-scoped.
- Alcune classi usano `sealed` quando rappresentano componenti concreti non estesi nel codice attuale.

## Gestione UI

- La UI e gestita tramite eventi WPF nel code-behind.
- I controlli vengono aggiornati direttamente tramite i loro nomi XAML.
- Le liste vengono popolate assegnando `ItemsSource`.
- I messaggi di stato utente vengono scritti in `StatusText` e `SelectedSourceText`.
- Il code-behind della UI puo orchestrare il login, ma non deve chiamare direttamente endpoint Firebase.

## Stile XAML

- Colori e brush principali sono definiti in `Window.Resources`.
- I pulsanti principali e secondari hanno stili dedicati.
- La schermata usa una struttura a `Grid`, `Border`, `StackPanel`, `ListBox` e `ListView`.
- Le icone UI sono rappresentate con glyph del font `Segoe MDL2 Assets`.
- Il logo e incluso come risorsa WPF in `Resources/dsc_logo.png`.

## Provider e astrazioni

- I provider di dispositivi implementano `IDeviceProvider`.
- Il provider locale implementa `IFlightLogProvider`.
- Il provider MTP per i Flight Log e concreto e non implementa `IFlightLogProvider`.
- L'autenticazione cloud passa attraverso `ICloudAuthenticationService`.
- L'import dei Flight Log passa attraverso `IFlightLogImportService`.
- Le implementazioni Firebase e DSC Cloud Functions restano nel progetto Core.
- La UI istanzia direttamente i provider concreti e il servizio cloud, senza dependency injection.

## Error handling osservato

- `LoadDevicesAsync` cattura `Exception` e aggiorna lo stato UI.
- `LoadFlightLogs` cattura `Exception` e resetta lista/statistiche.
- `FirebaseAuthenticationService` restituisce `AuthenticationResult` con messaggio di errore invece di propagare errori di login alla UI.
- `MtpFlightLogProvider` cattura `COMException` e restituisce una lista vuota.
- `PortableDeviceExplorer.GetObjectSize` cattura `COMException`, scrive su `Debug` e restituisce `0`.

## Logging osservato

- Esiste un'interfaccia `ILogger` con metodo `Write`.
- `DebugLogger` scrive su `System.Diagnostics.Debug`.
- `LocalFolderFlightLogProvider` usa `ILogger`.
- Il resto del flusso principale usa testi UI o `Debug.WriteLine`.

## Dati e binding

- `Device` e usato per popolare `DevicesList`.
- `FlightLogFile` e usato per popolare `FlightLogsList`.
- `CloudUser` rappresenta l'utente Firebase autenticato.
- `AuthenticationResult` rappresenta l'esito di login.
- Il binding XAML usa principalmente `Name` e `FileName`.
- Le statistiche vengono aggiornate manualmente in code-behind.

## Sicurezza osservata

- Le password non vengono raccolte dalla desktop app.
- Email/password e Google sono gestiti dal portale web DSC.
- Il refresh token viene salvato in Windows Credential Manager.
- L'ID token resta in memoria.
- `GetIdTokenAsync` e il punto previsto per ottenere il Firebase ID Token destinato alle Cloud Functions.
- Prima di caricare un Flight Log si calcola lo SHA-256 del contenuto reale e si consulta il cloud.
- La cronologia locale dei log deve restare una cache: il registro cloud DSC resta autoritativo.

## Dipendenze esterne osservate

- `Vanara.PInvoke.PortableDeviceApi` per le API Windows Portable Device.
- `Microsoft.Windows.CsWin32` come dipendenza privata di build/analyzers/native tooling.
- Riferimenti COM a `PortableDeviceApiLib` e `PortableDeviceTypesLib`.
- Endpoint REST Firebase Authentication e Cloud Function DSC usati tramite `HttpClient`, senza SDK Firebase aggiuntivi.
- Upload Flight Log tramite `multipart/form-data` e campo `logfile`.

## Build osservata

- `dotnet build` non e adatto a questa soluzione quando deve risolvere i riferimenti COM WPD.
- La build e stata verificata con MSBuild di Visual Studio:

```text
C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe
```

- La build passa con warning CA1416 esistenti sulle API Windows Portable Device.

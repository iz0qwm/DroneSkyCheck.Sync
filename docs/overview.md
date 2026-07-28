# DroneSkyCheck.Sync - Overview

## Scopo attuale

`DroneSkyCheck.Sync` e una soluzione .NET/WPF per individuare origini locali o MTP collegate al PC, mostrare Flight Log DJI disponibili e collegare l'app desktop all'Account Cloud Drone Sky Check tramite Firebase Authentication.

Il codice attuale implementa:

- una finestra desktop WPF con branding Drone Sky Check;
- rilevamento di unita disponibili tramite `DriveInfo`;
- rilevamento di dispositivi MTP tramite Windows Portable Device API;
- ricerca di file `.txt` DJI in un percorso MTP specifico;
- visualizzazione dei dispositivi e dei log trovati;
- autenticazione Account Cloud DSC tramite portale web DSC;
- supporto a email/password e Google attraverso il portale web DSC;
- callback desktop tramite schema `droneskycheck-sync://auth/callback`;
- riscatto del codice desktop tramite Cloud Function DSC;
- recupero e refresh del Firebase ID Token;
- logout dell'account cloud;
- aggiornamento della UI dopo login/logout con nome utente e piano;
- trasferimento dei Flight Log DJI verso DSC tramite Cloud Functions;
- controllo cloud dei duplicati prima dell'upload tramite SHA-256;
- salvataggio del refresh token in Windows Credential Manager.

Il codice attuale non implementa:

- verifica reale della membership DSC+ tramite backend;
- lettura dei Flight Log da unita locali nella UI;
- persistenza locale autoritativa dello stato dei log gia sincronizzati;
- salvataggio persistente della sessione cloud.

## Soluzione

La soluzione e definita in `DroneSkyCheck.Sync.slnx` e contiene quattro progetti:

- `DroneSkyCheck.Sync`: applicazione WPF desktop.
- `DroneSkyCheck.Sync.Core`: logica di rilevamento dispositivi, provider Flight Log, servizi cloud e integrazione Windows Portable Device.
- `DroneSkyCheck.Sync.Models`: modelli dati condivisi, inclusi i modelli Cloud.
- `DroneSkyCheck.Sync.Common`: progetto comune attualmente senza sorgenti applicativi propri.

## Runtime e piattaforma

I progetti usano .NET 10:

- UI: `net10.0-windows` con `UseWPF=true`.
- Core, Models, Common: `net10.0`.

La parte MTP dipende da API Windows e da riferimenti COM/Vanara, quindi la funzionalita principale di esplorazione dispositivi e legata all'ambiente Windows.

## Account Cloud DSC

L'autenticazione e implementata nel progetto Core, non nel code-behind della finestra principale.

`FirebaseAuthenticationService` usa il flusso desktop del portale DSC:

- apertura browser su `https://droneskycheck-d0136.web.app/sync-login.html`;
- redirect verso `droneskycheck-sync://auth/callback`;
- verifica dello `state`;
- POST a `redeemDesktopAuthCode`;
- scambio del `customToken` con Firebase REST `accounts:signInWithCustomToken`;
- refresh token: `securetoken.googleapis.com/v1/token`.

La redirect URI desktop e:

```text
droneskycheck-sync://auth/callback
```

L'applicazione non salva password. Il portale gestisce email/password e Google. La desktop app salva il refresh token in Windows Credential Manager e usa l'ID token Firebase in memoria per le chiamate cloud.

## Trasferimento Flight Log DSC

Il trasferimento dei file DJI `.txt` viene gestito dal progetto Core tramite `DjiFlightLogImportService`.

Prima di caricare un log, la desktop app:

1. recupera un Firebase ID Token valido con `GetIdTokenAsync`;
2. risolve l'operatore UAS attivo da `CloudUser.OperatorId`, con fallback stabile a `CloudUser.Uid`;
3. legge il contenuto reale del file dal dispositivo MTP;
4. calcola lo SHA-256 del contenuto;
5. chiama `checkDjiFlightLogImport` con token Firebase, operatorId, SHA-256, dimensione e nome file;
6. carica il file su `importDjiFlightLog` solo se il cloud risponde che non e gia importato.

Endpoint usati:

- `https://europe-west8-droneskycheck-d0136.cloudfunctions.net/checkDjiFlightLogImport`
- `https://europe-west8-droneskycheck-d0136.cloudfunctions.net/importDjiFlightLog`

L'upload usa `multipart/form-data` con campo `logfile` e invia gli header `Authorization: Bearer <Firebase ID token>`, `X-DSC-Operator-ID` e `X-DSC-Log-SHA256`. Se `importGateway.duplicate` e presente nella risposta, il file e stato ricevuto ma rappresenta un volo gia noto.

La cronologia locale, quando verra introdotta o estesa, deve restare una cache. Il registro cloud DSC resta l'autorita per file gia importati e voli duplicati.

## Flusso utente attuale

1. All'avvio WPF apre `MainWindow.xaml`.
2. `MainWindow_Loaded` imposta lo stato account non connesso.
3. La UI avvia la ricerca dei dispositivi collegati.
4. Vengono combinati i risultati di `DriveDeviceProvider` e `MtpDeviceProvider`.
5. L'utente seleziona una sorgente dalla lista.
6. Se la sorgente e MTP, `MtpFlightLogProvider` cerca i Flight Log DJI.
7. I log trovati vengono mostrati nella lista e conteggiati come nuovi.
8. Il pulsante `Accedi` apre `CloudLoginWindow`.
9. `CloudLoginWindow` apre il portale web DSC nel browser.
10. Dopo il login web, il callback desktop viene riscattato dal servizio Core.
11. Dopo il login, la UI mostra stato connesso, nome utente e piano.
12. Se sono presenti log e l'account risulta DSC+, `TransferButton` abilita il trasferimento.
13. Il trasferimento controlla il cloud, salta i log gia importati e carica solo quelli mancanti.
14. Cliccando sul nome utente viene proposta la disconnessione.

# Roadmap

Questo documento riassume lo stato operativo e i punti incompleti visibili nel codice attuale.

## Presente e funzionante nel codice

- Avvio applicazione WPF tramite `App.xaml`.
- Finestra principale con layout per account cloud, origini Flight Log, lista log, statistiche e stato inferiore.
- Rilevamento drive tramite `DriveInfo.GetDrives()`.
- Rilevamento dispositivi MTP tramite Windows Portable Device API.
- Esplorazione MTP tramite `PortableDeviceExplorer`.
- Ricerca Flight Log DJI nel percorso `Android/data/dji.go.v5/files/FlightRecord`.
- Filtro sui file `.txt` con prefissi `DJIFlightRecord_` e `FlightRecord_`.
- Conteggio log trovati e nuovi nella UI.
- Reset delle statistiche quando non c'e selezione o quando un'origine non e supportata.
- Login Account Cloud DSC tramite portale web DSC.
- Supporto email/password e Google delegato al portale web DSC.
- Callback desktop `droneskycheck-sync://auth/callback`.
- Scambio `customToken` con Firebase REST.
- Recupero e refresh del Firebase ID Token in memoria.
- Salvataggio del refresh token in Windows Credential Manager.
- Logout account cloud.
- Stato UI aggiornato dopo login/logout con nome utente e piano.
- Lettura bytes dei Flight Log MTP tramite Windows Portable Device resources.
- Calcolo SHA-256 del contenuto prima del trasferimento.
- Check cloud dei log gia importati tramite `checkDjiFlightLogImport`.
- Upload dei log mancanti tramite `importDjiFlightLog`.
- Gestione dei duplicati di volo segnalati da `importGateway.duplicate`.

## Presente ma non collegato al flusso principale

- `LocalFolderFlightLogProvider` cerca file `.txt` in cartelle locali, ma `MainWindow` non lo usa quando viene selezionato un dispositivo non MTP.
- `IFlightLogProvider` definisce un contratto asincrono, ma `MtpFlightLogProvider` espone un metodo sincrono e non implementa l'interfaccia.
- `DebugLogger` esiste ed e usato dal provider locale, ma non dal flusso MTP o dalla UI principale.
- `DroneSkyCheck.Sync.Common` e referenziato, ma non contiene codice applicativo proprio.

## Stati UI e funzionalita non ancora operative

- La membership DSC+ reale non viene ancora interrogata dal backend.
- Gli utenti Firebase autenticati sono temporaneamente considerati DSC+ dal servizio di autenticazione.
- `TransferButton` avvia il flusso di check/import quando i prerequisiti UI sono soddisfatti.
- `ImportedLogsValueText` viene aggiornato durante il trasferimento contando log caricati, gia presenti o duplicati lato gateway.

## Limiti e comportamenti attuali

- Le origini locali vengono rilevate ma non analizzate dalla UI.
- La ricerca MTP intercetta `COMException` e restituisce una lista vuota, senza propagare dettagli al chiamante.
- `MtpFlightLogProvider` valorizza `FullPath` con il nome del file, non con un percorso MTP completo.
- `LastModifiedUtc` non viene valorizzato dal provider MTP.
- Non sono presenti test automatizzati nella soluzione.
- La Firebase Web API Key e configurata nel servizio Core.
- Il refresh token viene salvato in Windows Credential Manager.
- L'ID token resta in memoria ed e rinnovato tramite refresh token.
- La cronologia locale dei log sincronizzati non e autoritativa: il cloud DSC resta la fonte ufficiale.

## Sequenza incompleta gia rappresentata nel codice

Il codice contiene una traccia chiara di un percorso di sincronizzazione:

1. rilevare dispositivi;
2. selezionare una sorgente;
3. trovare log;
4. richiedere account DSC tramite portale web DSC;
5. ottenere il Firebase ID Token;
6. abilitare trasferimento solo in presenza dei prerequisiti.
7. calcolare SHA-256 del file;
8. chiedere al cloud se il file e gia importato;
9. caricare solo i file mancanti.

Nel codice attuale i punti 1-9 sono implementati per MTP e Account Cloud. La verifica DSC+ reale resta da collegare al backend DSC.

## Prossimi passaggi gia implicati dallo stato corrente

- Collegare la verifica membership DSC+ a una Cloud Function DSC usando il Firebase ID Token.
- Sostituire il fallback `CloudUser.Uid` con un resolver ufficiale dell'operatore UAS attivo se richiesto dal backend.
- Valutare persistenza sicura della sessione solo se richiesta, senza salvare password.

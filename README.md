# Drone Sky Check Sync

Drone Sky Check Sync è l'applicazione Windows collegata a **Drone Sky Check+ (DSC+)** per acquisire i registri di volo DJI dai dispositivi collegati al PC e trasferirli al profilo dell'utente.

## A cosa serve

Permette di raccogliere più Flight Log senza copiarli e caricarli uno per uno dal browser. I log vengono inviati al servizio DSC+ e alimentano lo stesso Registro della flotta utilizzato dall'importazione manuale web: non viene creato un registro separato.

Il programma legge i file, controlla quelli già importati e mostra l'avanzamento con risultati, duplicati, richieste di revisione ed eventuali errori. Non cancella i log dal dispositivo e non crea un archivio locale di backup.

## Collegamento con Drone Sky Check

Sync è un'applicazione complementare al [portale Drone Sky Check](https://droneskycheck-d0136.web.app/). L'accesso avviene dal portale nel browser e torna all'app Windows. Per trasferire i log serve un account DSC+ abilitato all'importazione. I risultati si consultano nel Pilot Workspace e nel Registro della flotta DSC+.

L'importazione manuale dal browser rimane disponibile per i file già presenti sul PC o sullo smartphone.

## Stato della distribuzione

La beta **0.1.0-beta.1** è in preparazione locale. Non è ancora disponibile una release pubblica da scaricare. Non sono richiesti acquisti o installazioni di Visual Studio per usare il futuro pacchetto Windows self-contained.

## Dispositivi e file

Sync usa USB/MTP e cerca nella cartella DJI Fly `Android/data/dji.go.v5/files/FlightRecord` i file `.txt` con prefisso `DJIFlightRecord_` oppure `FlightRecord_`.

Il manutentore riferisce prove ripetute riuscite di acquisizione e invio con DJI Air 3S, Avata 360 con DJI RC2, Mini 3 Pro con DJI RC e DJI Fly su smartphone. Queste prove non garantiscono ogni combinazione di dispositivo, firmware e versione dell'app; il modello dello smartphone non è specificato.

La cartella deve essere visibile al PC: sbloccare il dispositivo e abilitare il trasferimento file USB. Le cartelle locali e le unità del PC non sono ancora selezionabili come sorgente nell'interfaccia Sync. Non è dichiarato supporto per iPhone/iOS, DAT, CSV, INAV, Betaflight o ArduPilot.

## Installazione e utilizzo della beta

Istruzioni per il pacchetto quando sarà distribuito:

1. Usare un PC Windows x64; Windows 11 è il target principale. Windows 10 richiede verifica della specifica edizione/versione.
2. Verificare lo SHA-256 dello ZIP con `Get-FileHash <file.zip> -Algorithm SHA256` e confrontarlo con `SHA256SUMS.txt`.
3. Estrarre l'intero ZIP in una cartella stabile del proprio utente e avviare `app/DroneSkyCheck.Sync.exe`. Il runtime .NET è incluso; non spostare soltanto l'eseguibile.
4. Collegare il dispositivo con un cavo dati, consentire USB/MTP e aggiornare l'elenco dispositivi.
5. Accedere a DSC dal browser, selezionare i log e avviare il trasferimento.
6. Controllare il riepilogo e i voli nel Registro della flotta DSC+.

Serve una connessione Internet. La beta locale non è firmata digitalmente; la verifica su PC senza strumenti di sviluppo è ancora da completare.

## Limiti e sessione

Il limite applicativo è 50 MiB per log. Alcuni errori MTP possono apparire come elenco vuoto: controllare cavo, permessi e cartella. In caso di limitazione delle richieste il trasferimento attende e riprende; è disponibile Stop.

La sessione è conservata in Gestione credenziali Windows. Il login registra il protocollo `droneskycheck-sync` per l'utente Windows, associato al percorso dell'eseguibile. Eseguire logout prima di rimuovere l'app. La cancellazione della cartella non rimuove automaticamente il protocollo; non è ancora disponibile un disinstallatore dedicato. Non condividere credenziali o log personali nelle segnalazioni.

## Tecnologia

C#, WPF e .NET 10; soluzione `DroneSkyCheck.Sync.slnx`. La compilazione richiede SDK .NET 10 e MSBuild completo di Visual Studio/Build Tools con supporto COM Windows. Questi strumenti servono allo sviluppo, non all'uso del pacchetto self-contained.


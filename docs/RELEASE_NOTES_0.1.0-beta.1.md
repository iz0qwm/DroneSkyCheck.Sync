# Drone Sky Check Sync 0.1.0-beta.1

Beta preparata in locale, non ancora pubblicata.

Applicazione Windows x64 per acquisire Flight Log DJI via USB/MTP e trasferirli al profilo DSC+, con login nel browser, controllo duplicati e riepilogo per file. Alimenta lo stesso Registro della flotta dell'importazione manuale web.

Distribuzione ZIP self-contained: estrarre l'intera cartella, verificare SHA-256 e avviare `app/DroneSkyCheck.Sync.exe`. .NET incluso; Visual Studio e SDK non necessari all'utente finale. Windows 11 è il target principale; compatibilità Windows 10 da verificare per edizione/versione. Pacchetto non firmato.

Prove riferite dal manutentore: DJI Air 3S, Avata 360 con DJI RC2, Mini 3 Pro con DJI RC e DJI Fly su smartphone. Nessuna estensione implicita ad altri modelli o firmware.

Limiti: cartella DJI Fly accessibile via MTP, TXT con prefissi DJIFlightRecord_ o FlightRecord_; cartelle locali non selezionabili; nessun backup locale o cancellazione dei file originali; limite applicativo 50 MiB; alcuni errori MTP possono produrre un elenco vuoto. Account DSC+ abilitato e Internet necessari. Nessun installer o aggiornamento automatico; protocollo Windows e credenziali richiedono attenzione alla rimozione, come descritto nel README.

Questa beta introduce il versionamento e il confezionamento Windows del codice esistente. Non modifica protocolli, autenticazione o backend. Test di acquisizione/invio già effettuati dal manutentore; resta da completare la prova del pacchetto su PC pulito senza strumenti di sviluppo.

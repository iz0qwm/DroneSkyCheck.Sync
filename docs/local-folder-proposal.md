# Selezione cartella locale — proposta, non implementata

`LocalFolderFlightLogProvider` è un componente isolato: `MainWindow` non lo istanzia e `LoadFlightLogs` interrompe esplicitamente la lettura quando `Device.IsMtp` è falso. L'elenco unità non è un selettore di cartella. Il provider cerca tutti i TXT ricorsivamente e restituisce Task.FromResult dopo enumerazione sincrona. Non gestisce singolarmente cartelle non accessibili, junction o file scomparsi. Queste differenze spiegano perché la sola presenza della classe non rende disponibile la funzione.

La lettura locale dei byte è già prevista in `ReadFlightLogBytesAsync`: FullPath, esistenza file, File.ReadAllBytesAsync. È quindi possibile attivare una sorgente locale mantenendo il flusso cloud comune, senza modificare USB/MTP, protocolli o backend.

Proposta per una versione successiva:

1. Aggiungere un'azione distinta «Scegli cartella sul PC» con Microsoft.Win32.OpenFolderDialog WPF. Annullare il dialogo lascia selezione e lista precedenti intatte. Non scandire automaticamente tutte le unità.
2. Rappresentare la scelta come sorgente locale esplicita con RootPath e IsMtp=false, evitando di confonderla con un dispositivo rilevato. Mantenere invariato il ramo MTP.
3. Eseguire scansione fuori dal thread UI con CancellationToken. Applicare lo stesso filtro nomi/estensione DJI; rendere esplicita la scelta di includere sottocartelle. Non seguire reparse point/junction e distinguere cartella vuota da accesso negato.
4. Popolare FlightLogFile con FullPath assoluto, dimensione e data; azzerare selezioni/statistiche/hash della sorgente precedente. Disabilitare il cambio sorgente durante trasferimento.
5. Riutilizzare selezione, controlli di dimensione, SHA-256 e upload esistenti. Rileggere e verificare file prima dell'invio, gestire modifiche/scomparsa e impedire letture senza limite. Non spostare né cancellare originali.
6. Test con directory temporanee: TXT valido, nome non DJI, maiuscole, annullamento, cartella vuota, accesso negato, sottocartelle, junction, file rimosso/cambiato, limite dimensione. Regressione MTP e alternanza locale/MTP con i dispositivi già provati.

Impatto circoscritto a UI, selezione sorgente e provider locale; nessuna migrazione dati. Rischi principali: blocco UI, scansioni troppo ampie, accessi negati e race fra scansione e lettura. Non attivare semplicemente il provider sul drive root. La beta 0.1.0-beta.1 mantiene il comportamento attuale.

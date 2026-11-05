# Drone Sky Check Sync — audit distribuzione Windows

Data: 5 novembre 2026. Stato: preparazione locale completata; nessuna pubblicazione, push, modifica di visibilità GitHub o deploy DSC+. Protocolli, autenticazione, backend e codice applicativo invariati.

## Esito

L'applicazione compila in Release ed è stato generato uno ZIP self-contained Windows x64 con runtime incluso. Il proprietario conferma prove ripetute riuscite di acquisizione e invio log. Non si tratta quindi di una funzionalità soltanto ipotizzata dal codice. Restano separati i test hardware riferiti dal proprietario, la verifica statica svolta nell'audit e i test non ancora eseguiti su macchina pulita.

Modelli/configurazioni dichiarati dal proprietario: DJI Air3S, Avata 360 con DJI RC2, Mini 3 Pro con DJI RC e DJI Fly su smartphone. Nomi riportati come comunicati, senza inferire ulteriori abbinamenti. Non sono disponibili in questo audit firmware, versione DJI Fly, sistema operativo e modello dello smartphone né una matrice di risultati per ciascun log. Presentarli come configurazioni provate dal manutentore, non come garanzia universale.

## Progetto e tecnologia

- Sync: `C:\Users\raffa\VisualStudio\DroneSkyCheck.Sync`.
- DSC+: `C:\Users\raffa\DroneSkyCheck`, analizzato in sola lettura.
- Soluzione Visual Studio `DroneSkyCheck.Sync.slnx`, quattro progetti: UI, Core, Models, Common.
- C# e XAML/WPF; UI `net10.0-windows`, librerie `net10.0`. Nullable e implicit usings attivi. Configurazioni SDK standard Debug/Release; nessuna configurazione speciale nella soluzione.
- SDK rilevato 10.0.401, MSBuild di Visual Studio 18 Community. Nessun global.json né versione applicativa esplicita nei progetti originali.
- Core: Microsoft.Windows.CsWin32 0.3.298, Vanara.PInvoke.PortableDeviceApi 5.0.5; riferimenti COM PortableDeviceApiLib e PortableDeviceTypesLib, Windows Portable Device API, Credential Manager e registro HKCU.

## Git e GitHub

Repository locale inizialmente pulito, branch `master`, HEAD `3c6e301cc4ab05268fd2510478d10ae1a933fe2d`; riferimento locale `origin/master` allo stesso commit. Remote fetch/push: `https://github.com/iz0qwm/DroneSkyCheck.Sync.git`. Quattro commit raggiungibili dalle ref locali, nessun tag locale. Non è stato effettuato fetch: l'allineamento è rispetto alla ref locale, non una verifica del remoto attuale.

Repository GitHub privato, confermato dal proprietario. La richiesta in sola lettura a `https://api.github.com/repos/iz0qwm/DroneSkyCheck.Sync/releases` tramite connettore ha restituito 404: accesso non disponibile con questa connessione. Esistenza e numero di release precedenti non verificabili; assenza di tag locali non dimostra assenza di release remote.

## Funzioni verificate nel codice

| Funzione | Evidenza e limite |
| --- | --- |
| Riconoscimento dispositivi | `DriveDeviceProvider` enumera unità Windows; `MtpDeviceProvider` usa `PortableDeviceHelper` per dispositivi MTP. Nessuna whitelist di modelli certificati. |
| Ricerca log | `MtpFlightLogProvider.GetFlightLogs`: cartella `Android/data/dji.go.v5/files/FlightRecord`, file `.txt` con prefisso `DJIFlightRecord_` oppure `FlightRecord_`, confronto case-insensitive. Non ricerca altri formati o tutte le cartelle DJI. |
| Cartelle locali | `LocalFolderFlightLogProvider` esiste e cerca ricorsivamente TXT, ma `MainWindow.LoadFlightLogs` rifiuta esplicitamente origini non MTP. Non dichiarare questa funzione disponibile all'utente. |
| Acquisizione | `PortableDeviceExplorer.ReadObjectBytes` legge lo stream MTP in memoria; il trasferimento usa i byte letti. Nessuna cancellazione originale o archivio locale di backup implementato nel flusso UI. |
| Login | `CloudLoginWindow` usa il browser DSC. `FirebaseAuthenticationService` genera state casuale, registra protocollo, attende callback, verifica state, riscatta codice e ottiene token Firebase. Login email/password presente nel servizio, non esposto dalla finestra attuale. |
| Sessione/profilo | Refresh token e contesto operatore nel Windows Credential Manager; rinnovo token, logout, UID/OperatorId/OwnerUid passati al servizio. Il piano client predefinito è DSC+: non costituisce prova di autorizzazione server. |
| Upload | `DjiFlightLogImportService` invia multipart `logfile` con Bearer, operatore, owner e SHA-256; endpoint di verifica singola/batch e import sono quelli DSC configurati. |
| Duplicati e risultati | SHA-256 nel client, controllo preventivo, gestione duplicati, persistenza registro e casi da revisionare. La conferma finale dipende dalle risposte server. |
| Errori e interruzione | Messaggi UI e risultati per file; ritardo minimo richieste di 8 secondi; 429 trasformato in pausa/ripresa dalla UI. Il metodo del servizio chiamato `SendWithRateLimitRetryAsync` non contiene un ciclo di retry: è la UI a riprovare. Il ciclo UI non applica il limite MaxRateLimitRetries configurato. |

## Compilazione e pacchetto

Build Release riuscita tramite MSBuild completo con restore. Log: `artifacts/audit-build-authorized.log`. Rilevati 53 avvisi CA1416 (API Windows in libreria target net10.0) e un MSB3026 transitorio di copia, recuperato. I primi tentativi nell'ambiente limitato hanno incontrato permessi/workload resolver; un tentativo concorrente ha prodotto CS2012 per file occupato. Il tentativo finale autorizzato ha completato la build; non sono stati modificati i sorgenti per aggirare errori.

Publish locale riuscita, exit code 0: `artifacts/audit-publish.log`. Parametri: Release, win-x64, SelfContained=true, PublishSingleFile=false, PublishTrimmed=false, DebugType=None, DebugSymbols=false, Version=0.1.0-audit.1. Procedura in `docs/windows-release.md`.

Output:

- `artifacts/windows-audit/app`: applicazione e runtime.
- `artifacts/DroneSkyCheck.Sync-0.1.0-audit.1-win-x64.zip`: pacchetto locale con README.
- `artifacts/SHA256SUMS.txt`: checksum dello ZIP.
- SHA-256: `a81760c1c446c6f9ebc6a4fd204f1bf97c669b6f4870aea7c9f7937f9edb57c5`.

Runtimeconfig verificato: Microsoft.NETCore.App e Microsoft.WindowsDesktop.App 10.0.12 inclusi. Visual Studio e SDK non sono requisiti di esecuzione previsti dal formato. La verifica definitiva rimane l'avvio su PC/VM pulita senza runtime preinstallato. Eseguibile `NotSigned`; nessuna firma applicata.

Consigliato ZIP self-contained con cartella completa: semplice per l'utente e meno variabili di packaging per COM/WPF. Installer per utente utile in seguito per aggiornamento e pulizia protocollo; single-file non collaudato. Windows 11 x64 target principale. Per Windows 10 non promettere genericamente tutte le edizioni: la documentazione Microsoft attuale limita il supporto alle edizioni/versioni indicate, incluse LTSC/Enterprise. Distinguere compatibilità tecnica e supporto del sistema.

## Segreti, dati personali e pubblicabilità

Ricerca statica nei sorgenti e nei quattro commit locali raggiungibili per chiavi, token GitHub, private key, assegnazioni sensibili, email e percorsi personali. Unico file segnalato dai pattern: `FirebaseAuthenticationOptions.cs`, nei due commit più recenti, per la chiave API Firebase client. Nessun token personale, password incorporata, service-account/private key rilevato nella scansione svolta. Non è una certificazione esaustiva: non comprende ref remote non presenti, oggetti irraggiungibili o contenuti amministrativi GitHub.

La chiave Firebase client è normalmente pubblicabile e non è una credenziale amministrativa; prima dell'apertura verificare restrizioni API, eventuale riutilizzo per altri servizi Google e autorizzazioni server. Non ne è stato riportato il valore in questo documento. Gli endpoint Production e project ID sono configurazione client, ma impediscono di trattare l'avvio con credenziali esistenti come test isolato. Nessun login o upload Production eseguito nell'audit.

Non trovati log di volo personali o certificati privati nell'inventario esaminato. Sono presenti `.vs`, bin/obj, `.csproj.user` e file generati con percorsi assoluti: esclusi dal tracciamento/pacchetto, da non pubblicare copiando l'intera cartella di sviluppo. Lo ZIP è stato creato solo dall'output publish e README; controllo file vietati (.pdb/.cs/.user/.pfx/.pem/.log/.env) = zero. I log di audit restano in artifacts ignorato. Anche questo report contiene percorsi locali: rivederlo prima di inserirlo in un repository pubblico.

Decisione consigliata rispetto alla richiesta del proprietario: la sola chiave Firebase trovata non richiede un secondo repository. Aprire l'originale è ragionevole dopo verifica restrizioni e revisione del contenuto remoto completo. Se emergono segreti reali, mantenerlo privato e creare un repository pubblico per release; rimuovere/ruotare comunque i segreti, perché un repository privato non rende sicuro un segreto incluso nell'eseguibile. Nessuna delle due operazioni è stata eseguita.

## Criticità da considerare

- MTP: COMException convertita in elenco vuoto, quindi errore e assenza di log non distinguibili; ricerca sincrona nel percorso UI, da provare con molti file.
- Lettura completa in memoria; limite 50 MiB controllato dalla UI, ma stream MTP non limitato internamente e senza cancellazione. Provare memoria e stop durante lettura/upload.
- `PortableDeviceExplorer.Close` azzera il riferimento senza chiamare esplicitamente Close sul dispositivo; verificare rilascio COM e riconnessioni ripetute.
- Registrazione protocollo legata al percorso eseguibile: evitare spostamenti dopo login; nessuna disinstallazione completa automatica.
- Nessuna suite di test o pipeline release nel repository; manca una decisione esplicita di licenza e gestione attribuzioni.
- Nessuna modifica ad autenticazione o backend proposta come correzione automatica. Eventuali interventi richiedono necessità documentata e approvazione.

## Test eseguiti e mancanti

Eseguiti dall'audit: ricognizione codice e Git; scansione statica corrente/storico locale; build Release; publish self-contained; verifica runtime incluso, firma assente, esclusione file di sviluppo dal pacchetto, generazione SHA-256. Nessun test automatico di comportamento cloud/UI eseguito; nessun test dispositivo fisico svolto dall'agente.

Eseguiti dal proprietario: più prove riuscite di prelievo e invio; configurazioni riferite all'inizio del report. Acquisire in futuro versioni firmware/app e dettaglio delle combinazioni, senza invalidare le prove già svolte.

Mancanti: avvio su Windows pulito e Windows 10 specifici; casi di errore/rete e autorizzazione con account di test; stress MTP, stop, file corrotti e oltre limite; mock HTTP e fixture sintetiche; upgrade/disinstallazione; firma, antivirus e licenze. Checklist dettagliata in `docs/windows-release.md`. L'app non è stata avviata nell'audit, poiché all'avvio ripristina la sessione e può contattare Production.

## Integrazione proposta DSC+

Sezione individuata: `public/index.html`, card `pilot-flight-log-card`, righe circa 838–880, nel Pilot Workspace > Droni. Logica in `public/js/fleet/fleet-workspace.js`; servizio `public/js/core/fleet/flight-log-import-service.js`. Il file input `.txt` e l'azione manuale già esistono. Il servizio web usa lo stesso endpoint import DSC; non occorre cambiare backend.

Proposta: aggiungere un blocco secondario sotto l'import manuale, conservandone ID e gestione eventi. Titolo «Drone Sky Check Sync per Windows». Testo: «Acquisisci i Flight Log DJI dai dispositivi collegati via USB/MTP e trasferiscili al tuo profilo DSC+. Sono letti i file DJI TXT dalla cartella DJI Fly accessibile al PC. Puoi continuare a importare manualmente i file già disponibili.»

Riportare separatamente le configurazioni provate dal manutentore, con le qualificazioni sopra. Non promettere supporto cartelle locali, iPhone o formati diversi. Prima della release mostrare soltanto «Distribuzione Windows in preparazione», senza link fittizi o pulsante disabilitato presentato come download. Solo dopo conferma di pagina e asset pubblici inserire «Versioni e download per Windows» verso la pagina Releases scelta. L'URL del repository privato non è un download pubblico.

Layout: singola colonna su smartphone, nessuna larghezza fissa, testo che va a capo, stessi token CSS e pulsanti del riquadro esistente, focus tastiera visibile e contrasto chiaro/scuro. Su mobile spiegare che Sync si usa sul PC, lasciando l'import manuale utilizzabile. Collaudare 320/375/768/1280 px, zoom 200%, tastiera, tema chiaro/scuro. Questa è una proposta: nessuna modifica UI né test visuale di un'integrazione inesistente dichiarato come eseguito.

Manuale `docs/user/dsc-plus/flight-log-import.md`: già cita Sync, ma la tabella promette cartelle locali non utilizzabili dalla UI corrente. Correggere questa affermazione e aggiungere requisiti, installazione ZIP, login, configurazioni provate, limiti MTP, checksum e link ufficiale quando pubblico. Aggiornare anche le pagine sviluppatore correlate se descrivono tale capacità come disponibile. Nessun file DSC+ modificato.

## Prima della pubblicazione

1. Confermare restrizioni della chiave Firebase e accesso al repository completo; controllare release remote precedenti e contenuti da rendere pubblici.
2. Scegliere apertura repository originale o repository release separato, licenza e URL ufficiale; ottenere GO esplicito prima delle modifiche GitHub.
3. Superare il collaudo su macchina pulita; registrare configurazioni già provate e risolvere eventuali problemi bloccanti emersi.
4. Stabilire versione pubblica (proposta iniziale `0.1.0-beta.1`, successivamente SemVer), firma digitale e note release; rigenerare pacchetto e checksum dopo ogni modifica/firma. Non pubblicare automaticamente il pacchetto audit.
5. Preparare e verificare integrazione e manuale DSC+ sulla base della proposta; deploy soltanto con GO esplicito.

## Fonti esterne

- [Firebase: gestione API key](https://firebase.google.com/docs/projects/api-keys): distinzione chiavi client e autorizzazione, restrizioni per altri servizi.
- [Microsoft: Windows e .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows): piattaforme supportate e runtime.
- [Microsoft: single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview): differenza fra bundling e runtime incluso.

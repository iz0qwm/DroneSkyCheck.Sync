# Preparazione Windows e pubblicazione futura

Nessun push, cambio di visibilità, creazione repository, release o deploy è autorizzato da questa procedura. Serve il GO esplicito.

## Formato

Preferire ZIP con cartella self-contained `win-x64`, runtime incluso, senza trimming. Riduce le variabili legate a WPF, COM ed estrazione di librerie native rispetto al singolo eseguibile. Un installer per utente è una seconda fase utile per aggiornamenti, protocollo e disinstallazione. Il singolo eseguibile va validato separatamente, non è stato preparato.

## Build riproducibile

Da PowerShell nella radice del repository, con MSBuild completo individuato tramite vswhere:

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
& $msbuild DroneSkyCheck.Sync.slnx /restore /p:Configuration=Release /m:1 /nr:false
if ($LASTEXITCODE -ne 0) { throw 'Build fallita' }
& $msbuild DroneSkyCheck.Sync\DroneSkyCheck.Sync.csproj /restore /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishSingleFile=false /p:PublishTrimmed=false /p:DebugType=None /p:DebugSymbols=false /p:Version=0.1.0-audit.1 /p:PublishDir="$PWD\artifacts\windows-audit\app\" /m:1 /nr:false
if ($LASTEXITCODE -ne 0) { throw 'Publish locale fallita' }
```

Eseguire una sola compilazione alla volta. Usare una cartella di output nuova per ogni release. `Publish` qui genera file locali: non pubblica su Internet. In questa macchina è stato necessario disabilitare il workload resolver tramite `MSBuildEnableWorkloadResolver=false`; non è un requisito dell'app per l'utente finale.

## Versioni, firma e GitHub

La versione `0.1.0-audit.1` identifica soltanto la preparazione locale. Proposta: prima beta `0.1.0-beta.1`, tag `v0.1.0-beta.1`, poi SemVer; `1.0.0` dopo collaudo. Fissare versione SDK, dipendenze e versione runtime nella procedura di release; una distribuzione self-contained richiede nuove build per aggiornare il runtime.

Prima della pubblicazione: stabilire licenza del progetto e attribuzioni dipendenze; decidere repository pubblico originale oppure repository separato per i binari. Confermare via accesso anonimo la pagina Releases prima di inserirla in DSC+. Non usare asset privati come download pubblico.

Firmare con certificato Authenticode dell'editore e timestamp, quando disponibile; verificare con `Get-AuthenticodeSignature`. Non è stata configurata o applicata una firma nell'audit. Generare ZIP e SHA-256 dopo la firma, pubblicare `SHA256SUMS.txt`, note versione, requisiti, limitazioni, cambiamenti e istruzioni. Non suggerire di disabilitare protezioni Windows per aprire un file sconosciuto.

## Collaudo richiesto

- VM pulita Windows 11 x64 senza SDK/Visual Studio/runtime installato; account standard; percorso con spazi e caratteri accentati.
- Windows 10 per ogni edizione/versione che si intende dichiarare compatibile. Supporto Microsoft e semplice eseguibilità non sono sinonimi.
- Hardware: modello, firmware, Android, DJI Fly, modalità USB, cartella visibile, elenco log e hash dei byte letti; scollegamento durante lettura, dispositivo bloccato, più dispositivi.
- Login browser, state errato/scaduto, callback con app aperta/chiusa, ripristino sessione, logout, piano non abilitato, operatore errato: account di test e ambiente approvato.
- Upload, duplicati, riconciliazione, 401/403/429/5xx, timeout, rete assente, stop, file vuoto/corrotto/oltre limite e memoria sotto carico.
- Test automatici isolati con HttpMessageHandler simulato per richieste/risposte cloud e fixture sintetiche per provider locale, senza chiamate Production.
- Verifica rimozione protocollo/credenziali, aggiornamento e rollback; verifica antivirus e firma finale.

Fonti: [distribuzione single-file Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), [Windows supportati](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

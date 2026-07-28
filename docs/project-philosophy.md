# DroneSkyCheck.Sync

DroneSkyCheck.Sync non è un parser di Flight Log.

Il suo scopo è:

- individuare automaticamente i dispositivi DJI;
- leggere i file di log;
- sincronizzarli con DSC Cloud;
- fornire un'interfaccia desktop moderna.

Il parsing dei Flight Log viene eseguito esclusivamente lato server.

La desktop application non deve contenere chiavi API di terze parti.

Tutte le funzionalità premium vengono abilitate dal backend DSC.

L'applicazione deve funzionare anche offline, limitando solo le funzionalità cloud.
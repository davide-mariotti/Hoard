<p align="center">
  <img src="icon/hoard.png" width="112" alt="Hoard icon">
</p>

<h1 align="center">Hoard</h1>

<p align="center">
  Shows what fills your disks: a map of every folder, coloured by kind of file, and a tray icon that speaks up when a drive runs out of room.<br>
  <a href="#english">English</a> · <a href="#italiano">Italiano</a> · <a href="https://davide-mariotti.github.io/apps/hoard">Website</a>
</p>

<p align="center">
  <a href="https://ko-fi.com/Z0X32ATNHL"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Buy me a coffee on Ko-fi"></a>
</p>

---

## English

### What it does

- Reads a whole drive, folder by folder: internal drives, USB sticks, network drives. On an NVMe a system drive with a million files takes under a minute the first time and a few seconds after that.
- Shows it as a map: every folder and file is a tile as big as the space it takes, coloured by what fills it (video, pictures, programs and games, archives and disk images, and so on). Click a folder to go in, right-click anything to see it in Explorer.
- Next to the map: what the folder holds by kind of file, its contents biggest first, and the 50 biggest files on the whole drive.
- Points at what can usually go: temporary files, Downloads, the Recycle Bin, Windows.old, crash dumps, the npm, Yarn, pip, NuGet and Gradle caches, every `node_modules`, the hibernation file. Each with one line on what it is and how to clear it.
- Tells you how much it couldn't see, and why: restore points, system files, folders closed to normal users. *Restart as administrator* reads most of them.
- Keeps an eye on free space from the tray: the Hoard icon while there's room, the free percentage in amber when a drive gets close to your limit and in red under it, plus one warning when it drops under (10% by default).
- Starts with Windows if you ask it to. Speaks Italian or English, following the language of Windows.

### What it doesn't do

- It doesn't delete anything. Hoard shows, Explorer deletes, with the Recycle Bin as a safety net. A program that only reads can't empty the wrong folder.
- It doesn't count anything twice: junctions and symbolic links are skipped, the folder they point to is counted where it really lives. OneDrive files kept "online only" count as zero, because they sit on the server, not on your disk.
- Files compressed by Windows and hard links count at full size, as in Explorer, so the Windows folder looks a little bigger than it is. When that adds up to something, Hoard says so.
- It doesn't keep a million files in memory: only the folders. The files of a folder are listed again when you open it. The scan of a full system drive takes about 50 MB of memory, and Hoard lets it go ten minutes after you close the window.
- It doesn't connect to the internet. No telemetry, no auto-updates, no account. The window is a page packed inside the exe, shown by WebView2, the Edge engine that comes with Windows 10 and 11. Settings live in `HKCU\SOFTWARE\HoardApp`, or in `Hoard.ini` in the portable version.
- It doesn't run on macOS, Linux, or 32-bit Windows.
- It doesn't make room by itself. It tells you where the space went; what to throw away is still your call.

### Download

1. Download [Hoard.exe](https://github.com/davide-mariotti/Hoard/raw/main/Hoard.exe) (1.1 MB). Windows 10 or 11, 64-bit, nothing else to install.
2. Put it in a folder that stays put (say `Documents\Tools`), not in Downloads.
3. Double-click it. The first time, SmartScreen may complain because the exe isn't signed: *More info* → *Run anyway*.
4. Pick a drive. For the folders closed to normal users: *Restart as administrator*, in the window or with a right-click on the tray icon. Hoard comes back and reads the same drive again.

**Portable version**: [Hoard-portable.zip](https://github.com/davide-mariotti/Hoard/raw/main/Hoard-portable.zip) (400 KB), the same exe with a `Hoard.ini` next to it. Unzip it anywhere, a USB stick too. As long as `Hoard.ini` sits next to the exe, the settings are saved in that file and the registry is never touched. That's also why this version can't start with Windows.

### How to use it

- The window opens on your drives: used space, free space, size. Click one to read it. While it reads you see the counters and the main folders growing; *Stop* takes you back.
- **The map**: the area of a tile is its size, the colour is the kind of file that fills it (legend underneath). Big folders show what's inside them too. Hover a tile for name, size and share. Click a folder to go in, click a file to select it, right-click either to see it in Explorer. Go back up with the arrow, the path at the top, or Backspace.
- **On the right**: *Folder* (what's in the folder you're looking at, by kind and by size), *Biggest* (the 50 biggest files on the drive: click one to find it on the map) and *Worth a look* (the folders listed above, with where they are).
- **In the tray**: the Hoard icon, or a number on amber or red when a drive is filling up. Hover it for the free space of every internal drive. Double-click for the window, right-click → *Exit* to close Hoard. Closing the window leaves Hoard in the tray.
- The gear at the top right opens the settings: start with Windows, the free space warning and its limit. They're saved as you change them.

### How it works

1. Hoard reads each folder with `FindFirstFileEx`, which hands over the name, size and attributes of every entry in one go, with no extra call per file. Sixteen folders are read at once.
2. Junctions and symbolic links are skipped, OneDrive's online-only files count as zero, paths longer than 260 characters work (`node_modules` goes deeper than that).
3. Only folders stay in memory. When you open a folder its files are listed again, so what you see is also up to date.
4. Sizes add up the tree, split by kind of file (from the extension), and the 100 biggest files are kept along the way.
5. Hoard compares what it found with the space the drive says it uses: the difference is what it couldn't read, and the notice at the top says so.

The eight colours come from a palette checked for colour-blind readers, plus grey for everything else. The tray checks the free space once a minute.

### Build from source

No Visual Studio needed: Windows already ships the C# compiler of .NET Framework 4.

```cmd
build.bat
```

You get `Hoard.exe` and `Hoard-portable.zip` next to the sources. The WebView2 libraries in `lib\` and the window in `ui\` (plain HTML, CSS and JavaScript, no build step) are packed inside the exe. `build.bat` runs `Hoard.exe --selftest`, which reads a small temporary folder tree with a junction in it and checks the sizes, the kinds of file, what the map shows and the folders worth a look. The icons come from `icon/make-icons.py` (Python, Pillow and Chrome), drawn with [Lucide](https://lucide.dev) glyphs, the same ones the window uses.

### Support

If Hoard found the 40 GB you'd forgotten about, you can [buy me a coffee on Ko-fi](https://ko-fi.com/Z0X32ATNHL). That's how the next app gets written.

### License

[MIT](LICENSE). The libraries and fonts inside the exe keep their own licenses (BSD, SIL OFL): the list is in [lib/README.md](lib/README.md).

---

## Italiano

### Cosa fa

- Legge un disco intero, cartella per cartella: dischi interni, chiavette USB, dischi di rete. Su un NVMe un disco di sistema con un milione di file ci mette meno di un minuto la prima volta e pochi secondi le volte dopo.
- Te lo mostra come una mappa: ogni cartella e ogni file è un riquadro grande quanto lo spazio che occupa, colorato secondo cosa lo riempie (video, immagini, programmi e giochi, archivi e immagini disco, e così via). Clic su una cartella per entrarci, tasto destro su qualsiasi cosa per vederla in Esplora file.
- Accanto alla mappa: cosa c'è nella cartella diviso per tipo di file, il suo contenuto dal più grande, e i 50 file più grandi di tutto il disco.
- Ti indica quello che di solito si può togliere: file temporanei, Download, Cestino, Windows.old, crash dump, le cache di npm, Yarn, pip, NuGet e Gradle, tutte le `node_modules`, il file di ibernazione. Ognuno con una riga su cos'è e su come si svuota.
- Ti dice quanto non è riuscito a vedere, e perché: punti di ripristino, file di sistema, cartelle chiuse agli utenti normali. *Riavvia come amministratore* ne legge quasi tutte.
- Tiene d'occhio lo spazio libero dalla tray: l'icona di Hoard finché c'è posto, la percentuale libera in arancione quando un disco si avvicina al tuo limite e in rosso quando lo supera, più un avviso quando ci scende sotto (10% di base).
- Si avvia con Windows, se glielo chiedi. Parla italiano o inglese, in base alla lingua di Windows.

### Cosa non fa

- Non cancella niente. Hoard mostra, Esplora file cancella, con il Cestino come rete di sicurezza. Un programma che legge e basta non può svuotare la cartella sbagliata.
- Non conta niente due volte: junction e collegamenti simbolici li salta, la cartella a cui puntano la conta dove sta davvero. I file di OneDrive tenuti "solo online" valgono zero, perché stanno sul server, non sul tuo disco.
- I file compressi da Windows e gli hard link contano per intero, come in Esplora file, quindi la cartella Windows sembra un po' più grande di quello che è. Quando la differenza diventa qualcosa, Hoard te lo dice.
- Non si tiene in memoria un milione di file: solo le cartelle. I file di una cartella li rilegge quando la apri. La lettura di un disco di sistema pieno occupa circa 50 MB di memoria, e Hoard la lascia andare dieci minuti dopo che hai chiuso la finestra.
- Non si collega a internet. Niente telemetria, niente aggiornamenti automatici, niente account. La finestra è una pagina chiusa dentro l'exe, mostrata da WebView2, il motore di Edge che c'è già in Windows 10 e 11. Le impostazioni stanno in `HKCU\SOFTWARE\HoardApp`, o in `Hoard.ini` nella versione portable.
- Non gira su macOS, Linux, né su Windows a 32 bit.
- Non fa spazio da solo. Ti dice dove è finito; cosa buttare resta una decisione tua.

### Scaricarlo

1. Scarica [Hoard.exe](https://github.com/davide-mariotti/Hoard/raw/main/Hoard.exe) (1,1 MB). Windows 10 o 11 a 64 bit, nient'altro da installare.
2. Mettilo in una cartella che non sposti (tipo `Documenti\Strumenti`), non in Download.
3. Doppio clic. La prima volta SmartScreen potrebbe lamentarsi perché l'exe non è firmato: *Ulteriori informazioni* → *Esegui comunque*.
4. Scegli un disco. Per le cartelle chiuse agli utenti normali: *Riavvia come amministratore*, nella finestra o col tasto destro sull'icona nella tray. Hoard si riapre e rilegge lo stesso disco.

**Versione portable**: [Hoard-portable.zip](https://github.com/davide-mariotti/Hoard/raw/main/Hoard-portable.zip) (400 KB), lo stesso exe con accanto un `Hoard.ini`. Lo scompatti dove vuoi, anche su una chiavetta. Finché `Hoard.ini` sta vicino all'exe, le impostazioni si salvano in quel file e il registro non viene mai toccato. Per lo stesso motivo questa versione non si avvia con Windows.

### Come si usa

- La finestra si apre sui tuoi dischi: spazio usato, spazio libero, dimensione. Clicca un disco per leggerlo. Mentre legge vedi i contatori e le cartelle principali che crescono; *Ferma* ti riporta indietro.
- **La mappa**: l'area di un riquadro è la sua dimensione, il colore è il tipo di file che lo riempie (la legenda è sotto). Le cartelle grandi mostrano anche cosa c'è dentro. Passa col mouse su un riquadro per nome, dimensione e quota. Clic su una cartella per entrarci, clic su un file per selezionarlo, tasto destro su tutti e due per vederli in Esplora file. Torni su con la freccia, con il percorso in alto o con Backspace.
- **A destra**: *Cartella* (cosa c'è nella cartella che stai guardando, per tipo e per dimensione), *Più grandi* (i 50 file più grandi del disco: clicca per trovarli sulla mappa) e *Da guardare* (le cartelle elencate sopra, con dove stanno).
- **Nella tray**: l'icona di Hoard, o un numero su arancione o rosso quando un disco si sta riempiendo. Passaci sopra col mouse per lo spazio libero di ogni disco interno. Doppio clic per la finestra, tasto destro → *Esci* per chiudere Hoard. Chiudere la finestra lascia Hoard nella tray.
- L'ingranaggio in alto a destra apre le impostazioni: avvio con Windows, l'avviso sullo spazio libero e il suo limite. Si salvano mentre le cambi.

### Come funziona

1. Hoard legge ogni cartella con `FindFirstFileEx`, che consegna nome, dimensione e attributi di tutto quello che c'è dentro in un colpo solo, senza una chiamata in più per ogni file. Legge sedici cartelle alla volta.
2. Salta junction e collegamenti simbolici, conta zero i file di OneDrive solo online, regge i percorsi più lunghi di 260 caratteri (`node_modules` va più in fondo di così).
3. In memoria restano solo le cartelle. Quando ne apri una, i suoi file li rilegge, quindi quello che vedi è anche aggiornato.
4. Le dimensioni salgono lungo l'albero, divise per tipo di file (dall'estensione), e intanto si tiene da parte i 100 file più grandi.
5. Confronta quello che ha trovato con lo spazio che il disco dice di usare: la differenza è quello che non ha potuto leggere, e l'avviso in alto te lo dice.

Gli otto colori vengono da una palette controllata anche per chi non distingue bene i colori, più il grigio per tutto il resto. La tray controlla lo spazio libero una volta al minuto.

### Compilarlo

Non serve Visual Studio: Windows ha già il compilatore C# di .NET Framework 4.

```cmd
build.bat
```

Ottieni `Hoard.exe` e `Hoard-portable.zip` accanto ai sorgenti. Le librerie di WebView2 in `lib\` e la finestra in `ui\` (HTML, CSS e JavaScript semplici, niente build) finiscono dentro l'exe. `build.bat` lancia `Hoard.exe --selftest`, che legge un piccolo albero di cartelle temporaneo con dentro una junction e controlla le dimensioni, i tipi di file, cosa mostra la mappa e le cartelle da guardare. Le icone le genera `icon/make-icons.py` (Python, Pillow e Chrome), con i glifi di [Lucide](https://lucide.dev), gli stessi della finestra.

### Supporto

Se Hoard ti ha fatto ritrovare i 40 GB di cui ti eri dimenticato, puoi [offrirmi un caffè su Ko-fi](https://ko-fi.com/Z0X32ATNHL). È così che nasce la prossima app.

### Licenza

[MIT](LICENSE). Le librerie e i font dentro l'exe hanno le loro licenze (BSD, SIL OFL): l'elenco è in [lib/README.md](lib/README.md).

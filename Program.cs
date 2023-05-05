using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;

namespace bwi40322
{
    class Program
    {
        //Config
        public const string URL = "https://cbrell.de/bwi403/demo/ZaehlerstandExport.csv"; //Die URL, von der die CSV-Datei heruntergeladen werden soll
        public const string CSV_DATEI_NAME = "ein.csv"; //Unter welchem Namen soll die CSV-Datei abgespeichert werden?
        public const string DATEN_ZWISCHENSPEICHER_DATEI_NAME = "aus.csv"; //Unter welchem Namen soll die Zwischenspeicherung im CSV-Format erfolgen?
        public const string KENNZAHLEN_DATEI_NAME = "kennzahlen.txt"; //Unter welchem Namen sollen die Kennzahlen im TXT-Format gespeichert werden?
        public const string VISUALISIERUNG_DATEI_NAME = "index.html"; //Unter welchem Namen soll die Visualisierung als HTML-Datei gespeichert werden?

        //Regex (= Regulärer Ausdruck) zur Erkennung eines gültigen Datums & Uhrzeit
        public const string DATUM_REGEX_PATTERN = @"^([1-9]|[12][0-9]|3[01])[.](1[0-2]|[1-9])[.]\d\d\s([1-9]|1[0-9]|2[0-4]):([0-5][0-9]|[0-9])$"; //Bsp. 28.12.2022 12:32

        //Records
        //Wir speichern alle Werte der CSV, um die langfristige Erweiterung des Programms zu vereinfachen --> Erzeugung weiterer Kennzahlen einfach möglich
        public record Datensatz(double? gasCBM, double? stromKWH, double? gasKWH, double? temperatur); //Simuliert eine Zeile aus der CSV-Datei
        public record Monatsverbrauch(double? gasCBM, double? stromKWH); //Dient zur gleichzeitigen Speicherung & leichten Abfrage der Kennzahlen

        static void Main(string[] args)
        {
            /* --------------------------------------------------
             Stage 1: Daten vom Webserver holen
            -----------------------------------------------------*/
            Console.WriteLine("--- Stage 1 ---");
            Console.WriteLine("[INFO] ETL-Prozess gestartet");
            Console.WriteLine("[INFO] CSV-Datei wird heruntergeladen");

            //Erstellen des WebClients, um die CSV-Datei herunterzuladen
            WebClient webClient = new WebClient();
            try
            {
                webClient.DownloadFile(URL, CSV_DATEI_NAME);
            }
            catch (Exception) //Beende das Programm, falls die CSV-Datei nicht heruntergeladen werden konnte
            {
                Console.WriteLine("[FEHLER] Daten konnten nicht heruntergeladen werden");
                return;
            }

            Console.WriteLine("[INFO] CSV-Datei erfoglreich heruntergeladen");
            Console.WriteLine("--- Stage 1 ---\r\n");
            /* --------------------------------------------------
             Stage 2: Daten von Lokal laden und aufbereiten
            -----------------------------------------------------*/
            Console.WriteLine("--- Stage 2 ---");
            /*Das Dictionary dient zur Speicherung der einzelnen Datensätze aus der CSV
            Das Datum dient dabei als Schlüssel und die restlichen Werte werden im Record Datensatz gespeichert*/
            Dictionary<String, Datensatz> datensaetze = new Dictionary<String, Datensatz>();
            string[] csvDatensaetze = File.ReadAllLines(CSV_DATEI_NAME); //CSV-Datei einlesen
            int anzahlDatensaetze = 0; //Anzahl an gültigen Datensätzen

            //Wenn die CSV-Datei nicht gefunden werden konnte, wird das Programm beendet
            if (!File.Exists(CSV_DATEI_NAME))
            {
                Console.WriteLine("[FEHLER] CSV-Datei konnte nicht gefunden werden");
                return;
            }

            Console.WriteLine("[INFO] CSV-Datei wird eingelesen");
            double? letzerZeitpunkt = 0; //Diese Variable dient dazu, um herauszufinden, ob die Datensätze der CSV in der richtigen Reihenfolge sind

            //Iteration über die einzelnen Zeilen der CSV + Überspringen der ersten Zeile, da in dieser diese nur die Spaltennamen stehen
            foreach (String zeile in csvDatensaetze.Skip(1))
            {
                String schluessel = zeile.Substring(0, zeile.IndexOf(";")); //Datum aus Zeile separieren, um ihn später als Key zu verwenden

                //Wenn das Datum nicht im regulären Format ist, wird die Zeile übersprungen
                if (!Regex.IsMatch(schluessel, DATUM_REGEX_PATTERN))
                {
                    Console.WriteLine($"[ACHTUNG] In der CSV-Datei ist ein nicht reguläres Datum enthalten. Fehler in der Zeile: ({zeile})");
                    continue;
                }

                String[] rohDaten = zeile.Substring(zeile.IndexOf(";") + 1).Split(';'); //Speichern der einzelnen Spalten als String, indem wir die Zeile am ; aufspalten
                if (rohDaten.Length != 5) //Wenn die Anzahl an Spalten nicht 5 ist, dann wird die Zeile übersprungen
                {
                    Console.WriteLine($"[ACHTUNG] Eine Zeile in der CSV-Datei hat nur {rohDaten.Length} von 5 Spalten. Die Zeile wurde nicht berücksichtigt. Fehler in der Zeile: ({zeile})");
                    continue;
                }

                double?[] konvertierteDaten = new double?[5]; //Vorbereitung, um die Daten als String in Doubles umzuwandeln
                for (int i = 0; i < rohDaten.Length; i++)
                {
                    double temp; //Dummy-Variable, der wir in der nächsten Zeile versuchen einen Wert zuzuweisen

                    //Wenn der Spalten-Wert in ein Double konvertiert werden kann, wird die Dummy-Variable dem Double-Array übergeben, ansonsten null --> Schlanker als Try-Catch
                    konvertierteDaten[i] = double.TryParse(rohDaten[i], out temp) ? temp : null;
                }

                //Falls die Datensätze in einer falschen Reihenfolge sind, wird das Programm beendet. (Auswertung per Excel-Zeitstempel)
                if (konvertierteDaten[0] < letzerZeitpunkt)
                {
                    Console.WriteLine($"[FEHLER] Daten nicht in der richtigen Reihenfolge. Fehler in der Nähe von: {schluessel}");
                    return;
                }
                letzerZeitpunkt = konvertierteDaten[0]; //Datum speichern für nächsten Vergleich

                //Daten im Record Datensatz speichern und dem Dictionary hinzufügen (Key: Datum, Value: Datensatz)
                datensaetze.Add(schluessel, new Datensatz(konvertierteDaten[1], konvertierteDaten[2], konvertierteDaten[3], konvertierteDaten[4]));
                anzahlDatensaetze++; //Anzahl an gültigen Datensätzen inkrementieren
            }

            Console.WriteLine($"[INFO] CSV-Datei erfolgreich eingelesen. Es wurden {anzahlDatensaetze} gültige Datensätze erkannt");
            Console.WriteLine("[INFO] Eingelesene Daten als JSON-Datei gespeichert");

            //Inhalt der in die Output-CSV geschrieben werden soll
            string inhalt = "Zeitstempel;Gas cbm kumuliert;Strom kWh kumuliert\n";
            foreach(KeyValuePair<string, Datensatz> datensatz in datensaetze)  {
                inhalt += $"{datensatz.Key};{datensatz.Value.gasCBM};{datensatz.Value.stromKWH};\n"; //Datensatz der CSV erzeugen und mit den bisherigen aus dem String verketten
            }
            File.WriteAllText(DATEN_ZWISCHENSPEICHER_DATEI_NAME, inhalt); //Speichern der Output-CSV-Datei Datei
            inhalt = ""; //Variable für späteren Gebrauch auf einen leeren Wert setzen --> Keine 2. Variable erzeugen --> Speicherplatz sparen

            Console.WriteLine("--- Stage 2 ---\r\n");
            /* --------------------------------------------------
             Stage 3: Daten transformieren, Kennzahl(en) erzeugen
            -----------------------------------------------------*/
            Console.WriteLine("--- Stage 3 ---");

            //Weiteres Dictonary instanziieren für die Kennzahlen mit Monatsname als Schlüssel
            Dictionary<string, Monatsverbrauch> kennzahlen = new Dictionary<string, Monatsverbrauch>();
            double? groessterWert = 0; //Finden der größten Kennzahl, um maximale Balken-Größe in der Visualisierung zu bestimmen (Verwendung in Stage 4)

            /*Liste mit den einzelnen Schlüsseln des Dictionary --> Dient vor allem zur Findung des nächsten Datums, falls der Wert von Strom oder Gas null ist.
            Zudem erhöhen wir die Geschwindigkeit des Programms, wenn wir die Liste nicht immer neu erzeugen müssen, sie sondern einfach den Methoden übergeben können*/
            List<String> schluesselListe = datensaetze.Keys.ToList();
            double? gasVerbrauch, stromVerbrauch; //Variable für Gas-Verbrauch & Strom-Verbrauch deklarieren --> Verwendung einer Variable für alle Berechnungen --> Speicherplatz sparen
            string finalesStartDatum, finalLastDate; //Variablen für endgültiges start- und End-Datum, da Wert am Stichtag null sein kann

            Console.WriteLine("[INFO] Kennzahlen werden erzeugt");

            //Ab hier beginnt der Code, um Anfang und Ende der Monate zu identifizieren

            string startDatum = schluesselListe.First(); //Datum des ersten Datensatzes ist erster Anfangs-Tag
            string endDatum = startDatum; //Erstes Datum der CSV ist zu Beginn gleichzeitig End-Datum
            foreach (string datum in schluesselListe)
            {
                string startMonat = startDatum.Split(".")[1]; //Monatsnummer des Start-Datums ermitteln für späteren Vergleich, ob neuer Monat im Datensatz begonnen hat
                string aktuellerMonat = datum.Split(".")[1]; //Monatsnummer des Datums vom derzeitigen Datensatz

                //Monatsende erkannt, durch unterschiedliche Monatsnummer oder wenn letzter Datensatz erreicht wurde
                if (aktuellerMonat != startMonat || (endDatum = datum) == schluesselListe.Last()) //Im 2. Fall Variable date der Variable lastDate zuweisen, um End-Datum für Berechnung zu definieren
                {
                    string monatsName = getMonatsName(startDatum); //Monatsnamen speichern, um ihn als Schlüssel für das Dictonary zu verwenden

                    //Wenn Start-Datum und End-Datum gleich sind, dann besteht der Monat nur aus einem Datensatz --> Verbrauch für Monat kann nicht bestimmt werden
                    if (endDatum == startDatum)
                    {
                        Console.WriteLine($"[ACHTUNG] Nicht genug Daten, um Verbrauch für {monatsName} zu bestimmen");
                        startDatum = datum;
                        continue;
                    }
                    Console.WriteLine($"[INFO] Verbrauch für {monatsName} wird berechnet (Stichtage: {startDatum} bis {endDatum})");

                    //startDatum & endDatum werden nicht überschrieben, da dies Verlauf des Programms stört
                    finalesStartDatum = validiereGasDatum(startDatum, false, datensaetze, schluesselListe); //In diesen Zeilen wird geschaut, ob Wert am Stichtag null ist
                    finalLastDate = validiereGasDatum(endDatum, true, datensaetze, schluesselListe);
                    gasVerbrauch = datensaetze[finalLastDate].gasCBM - datensaetze[finalesStartDatum].gasCBM; //Gas-Verbrauch berechnen
                    if(gasVerbrauch > groessterWert) groessterWert = gasVerbrauch; //Neue größte Kennzahl finden

                    //Stich-Tag für Strom und Gas kann variieren, da einer der beiden Werte vorhanden sein kann
                    finalesStartDatum = validiereStromDatum(startDatum, false, datensaetze, schluesselListe);
                    finalLastDate = validiereStromDatum(endDatum, true, datensaetze, schluesselListe);
                    stromVerbrauch = datensaetze[finalLastDate].stromKWH - datensaetze[finalesStartDatum].stromKWH; //Strom-Verbrauch berechnen
                    if(stromVerbrauch > groessterWert) groessterWert = stromVerbrauch; //Neue größte Kennzahl finden


                    //Verbrauch als Kennzahlen im jeweiligen Dictonary speichern per Record
                    kennzahlen.Add(getMonatsName(startDatum), new Monatsverbrauch(gasVerbrauch, stromVerbrauch));

                    //Wenn letzter Monat nur aus einem Datensatz besteht, kommt ein Fehler, da nicht genug Daten für die Ermittlung des Verbrauchs vorhanden sind
                    //Extra Fehlerbehandlung für den letzten Monat, da die Variable lastDate erst später aktualisiert wird und von der obigen Fehlerbehandlung nicht erkannt werden kann
                    if (datum == schluesselListe.Last() && !kennzahlen.ContainsKey(getMonatsName(datum)))
                    {
                        Console.WriteLine($"[ACHTUNG] Nicht genug Daten, um Verbrauch für {getMonatsName(datum)} zu bestimmen");
                        break;
                    }
                    startDatum = datum; //Derzeitiges Datum als Start-Datum festlegen, wenn Verbrauch für Monat berechnet wurde --> Indikator, dass neuer Monat begonnen hat
                }
                endDatum = datum; //Derzeitiges Datum als mögliches End-Datum setzen
            }

            inhalt = ""; //Dieser String wird später in die Kennzahlen-Datei geschrieben.
            
            //Durch alle Kennzahlen iterieren, die Nachricht für die Kennzahlen-TXT erzeugen und schließlich in die Datei schreiben
            foreach (KeyValuePair<string, Monatsverbrauch> monatsVerbrauch in kennzahlen)
            {
                //Der obigen Variable mit einer neuen Zeile verketten, die Auskunft über Verbrauch gibt. Werte sind hierbei auf 2 Nachkommastellen gerundet
                inhalt += $"Verbauch im {monatsVerbrauch.Key} lag bei {String.Format("{0:0.##}", monatsVerbrauch.Value.gasCBM)} m³ & {String.Format("{0:0.##}", monatsVerbrauch.Value.stromKWH)} kWh\n";
            }
            File.WriteAllText(KENNZAHLEN_DATEI_NAME, inhalt); //Verbrauch in txt-Datei schreiben

            Console.WriteLine("[INFO] Kennzahlen erfolgreich erzeugt");
            Console.WriteLine($"[INFO] Kennzahlen als {KENNZAHLEN_DATEI_NAME} gespeichert");
            Console.WriteLine("--- Stage 3 ---\r\n");
            /* --------------------------------------------------
             Stage 4: Ausgaben / Visualisierung erzeugen und speichern
            -----------------------------------------------------*/
            Console.WriteLine("--- Stage 4 ---");
            Console.WriteLine("[INFO] Daten werden visualisiert");

            string diagrammeHTMLCode = ""; //HTML-Code für die einzelnen Diagramme
            const int MAXIMALE_BALKEN_GROESSE = 200; //Maximale Länge eines HTML-Balkens in Pixel (Bei Änderung entsprechend im HTML-Code mitverändern)
            double? pixelProEinheit = MAXIMALE_BALKEN_GROESSE / groessterWert; //Angabe, wie viele Gas / Strom 1 Pixel in der Visualisierung repräsentiert

            //Diagramme erzeugen und miteinander als String verketten
            foreach (KeyValuePair<string, Monatsverbrauch> monatsVerbrauch in kennzahlen)
            {
                diagrammeHTMLCode += erstelleHTMLDiagramm(monatsVerbrauch.Key,monatsVerbrauch.Value,pixelProEinheit);
            }

            //Diagramme an entsprechende Stelle im HTML-Template einfügen
            string htmlCode = getHTMlCode().Replace("%Hier Diagramme einfügen%",diagrammeHTMLCode);
            File.WriteAllText(VISUALISIERUNG_DATEI_NAME,htmlCode); //HTML-Datei erzeugen

            Console.WriteLine("[INFO] Daten erfolgreich visualisiert");
            Console.WriteLine("[INFO] ETL-Prozess erfolgreich beendet");
            Console.WriteLine("--- Stage 4 ---\r\n");
        }
        /* --------------------------------------------------
         Zusätzliche Methoden
        -----------------------------------------------------*/

        //Diese Methode ermittelt aus einem Datum den Monatsnamen, indem sie auf die interne Monatsnamen-Liste von C# zugreift
        public static string getMonatsName(string date)
        {
            //Datum aufspalten an . und Leerzeichen, um auf die einzelnen Teile des Datum zugreifen zu können
            //Leerzeichen als zusätzliches Trennzeichen, da Uhrzeit von Datum mit Leerzeichen getrennt ist
            string[] aufgeteiltesDatum = date.Split(new Char [] {'.', ' '});
            return CultureInfo.CurrentCulture.DateTimeFormat.MonthNames[Int16.Parse(aufgeteiltesDatum[1]) - 1] + $" (20{aufgeteiltesDatum[2]})";
        }

        /*Diese Methode validiert ein Datum für die Berechnung des Gas-Verbrauchs, indem geschaut wird, ob der Wert am Stichtag ein null-Wert ist, 
        in diesem Fall wird der nächstmöglichste Datensatz genommen*/
        public static string validiereGasDatum(string date, Boolean searchPrevDate, Dictionary<String, Datensatz> datensaetze, List<string> datensaetzeKeys)
        {
            if (datensaetze[date].gasCBM != null) return date; //Wenn Wert nicht null ist, ist Datum valide
            int direction = (searchPrevDate) ? -1 : 1; //Bestimmt die Richtung, ob wir ein früheres oder späteres Datum suchen

            string newDate = date; //Neue Variable, da wir das alte Datum noch für die Benutzerausgabe benötigen

            //Solange in eine Richtung gehen und ein neues Datum suchen, bis der Wert nicht null ist
            while (datensaetze[newDate].gasCBM == null)
                newDate = datensaetzeKeys.ElementAt(datensaetzeKeys.IndexOf(newDate) + direction);

            Console.WriteLine("[ACHTUNG] Stich-Datum für Gas von " + date + " auf " + newDate + " geändert (Grund: Fehlender Wert)");
            return newDate;
        }

        /*Diese Methode validiert ein Datum für die Berechnung des Strom-Verbrauchs, indem geschaut wird, ob der Wert am Stichtag ein null-Wert ist, 
        in diesem Fall wird der nächstmöglichste Datensatz genommen*/
        public static string validiereStromDatum(string date, Boolean searchPrevDate, Dictionary<String, Datensatz> datensaetze, List<string> datensaetzeKeys)
        {
            if (datensaetze[date].stromKWH != null) return date; //Wenn Wert nicht null ist, ist Datum valide
            int direction = (searchPrevDate) ? -1 : 1; //Bestimmt die Richtung, ob wir ein früheres oder späteres Datum suchen

            string newDate = date; //Neue Variable, da wir das alte Datum noch für die Benutzerausgabe benötigen

            //Solange in eine Richtung gehen und ein neues Datum suchen, bis der Wert nicht null ist
            while (datensaetze[newDate].stromKWH == null)
                newDate = datensaetzeKeys.ElementAt(datensaetzeKeys.IndexOf(newDate) + direction);

            Console.WriteLine("[ACHTUNG] Stich-Datum für Strom von " + date + " auf " + newDate + " geändert (Grund: Fehlender Wert)");
            return newDate;
        }

        //Mit dieser Methode fügen wir die entsprechenden Kennzahlen in ein Diagramm-Template ein
        public static string erstelleHTMLDiagramm(string monatsName, Monatsverbrauch kennzahlen,double? pixelProEinheit) {
            return @$"<div class='karte'>
                            <div class='balken-diagramm'>
                            <div>
                                <div class='balken-wert'>{String.Format("{0:0.##}", kennzahlen.gasCBM)}</div>
                                <div class='einzelner-balken gas' style='height: {kennzahlen.gasCBM * pixelProEinheit}px'></div>
                            </div>
                            <div>
                                <div class='balken-wert'>{String.Format("{0:0.##}", kennzahlen.stromKWH)}</div>
                                <div class='einzelner-balken strom' style='height: {kennzahlen.stromKWH * pixelProEinheit}px'></div>
                            </div>
                            </div>
                            <div class='strich'></div>
                            <div class='monat'><p>{monatsName}</p></div>
                        </div>".Replace(",","."); //Wichtig, da Dezimalzahlen mit einem Punkt und nicht mit einem Komma dargestellt werden müssen
        }

        //Damit der HTML + CSS Code nicht mittendrin stört, haben wir diesen in eine Methode gepackt, um ihn abzufragen und danach zu verändern --> Erhöht die Lesbarkeit des Programms
        public static string getHTMlCode()
        {
            return @"<!DOCTYPE html>
                    <html lang='en'>
                    <head>
                    <meta charset='UTF-8' />
                    <meta http-equiv='X-UA-Compatible' content='IE=edge' />
                    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
                    <title>Monatsverbrauch von Gas & Strom</title>
                    </head>
                    <style>
                    html {
                        min-height: fit-content;
                        height: 100%;
                        font-family: Helvetica Neuen, Helvetica, Arial;
                    }

                    body {
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 100%;
                        height: fit-content;
                        width: 100%;
                        margin: 0;
                        background-image: url('https://images.pexels.com/photos/2860804/pexels-photo-2860804.jpeg?auto=compress&cs=tinysrgb&w=1260&h=750&dpr=1');
                        background-position: center;
                        background-attachment: fixed;
                        background-size: 100%;
                    }

                    .background-blur {
                        position: absolute;
                        z-index: -1;
                        width: 100%;
                        height: 100%;
                        backdrop-filter: blur(5px) brightness(80%);
                    }

                    section {
                        width: fit-content;
                        height: fit-content;
                        padding: 25px;
                        display: block;
                        align-items: center;
                        justify-content: center;
                        margin: 25px 0 25px 0;
                        background-color: rgb(255, 255, 255, 0.5);
                        backdrop-filter: blur(15px);
                        border-radius: 10px;
                        box-shadow: 0 0 80px rgba(0, 0, 0, 0.5);
                    }

                    h1 {
                        font-size: 2rem;
                        font-weight: 800;
                        margin: 0 0 25px 0;
                    }

                    .grid-container {
                        display: grid;
                        flex-direction: row;
                        justify-content: space-evenly;
                        grid-template-columns: repeat(4, 1fr);
                        grid-gap: 65px;
                        width: fit-content;
                    }

                    @media screen and (max-width: 700px) {
                        .grid-container {
                        grid-template-columns: 1fr;
                        margin-left: auto;
                        margin-right: auto;
                        }
                    }

                    .karte {
                        display: block;
                        position: relative;
                        width: 250px;
                        height: 300px;
                        transition: all 1s ease-out;
                        border-radius: 10px;
                        background: rgb(255, 255, 255, 0.3);
                        box-shadow: 0 0 70px rgba(0, 0, 0, 0.4);
                    }

                    .karte:hover {
                        transform: scale(1.1);
                        border: #006fff 0.2em solid;
                    }

                    .balken-diagramm,
                    .balken-wert {
                        display: flex;
                        flex-direction: row;
                        justify-content: space-evenly;
                    }

                    .balken-diagramm {
                        height: fit-content;
                        display: flex;
                        align-items: flex-end;
                        position: absolute;
                        bottom: 62px;
                        width: 100%;
                    }

                    .balken-wert {
                        width: 50px;
                        height: 20px;
                        text-align: center;
                        font-size: 1.5em;
                        font-weight: 600;
                    }

                    .einzelner-balken {
                        margin-top: 10px;
                        max-height: 200px;
                        width: 50px;
                        border-top-right-radius: 10px;
                        border-top-left-radius: 10px;
                    }

                    .einzelner-balken.gas {
                        background-color: #3AB795;
                    }

                    .einzelner-balken.strom {
                        background-color: #FFCF56;
                    }

                    .strich {
                        margin-left: 10%;
                        height: 3px;
                        border-radius: 1px;
                        position: absolute;
                        bottom: 60px;
                        width: 80%;
                        background-color: black;
                    }

                    .monat {
                        position: absolute;
                        bottom: 0;
                        width: 100%;
                        text-align: center;
                        font-size: 1.5em;
                        font-weight: 800;
                    }

                    .legende {
                        display: flex;
                        font-size: 1.1em;
                        font-weight: 550;
                        justify-content: space-evenly;
                        margin-left: auto;
                        margin-right: auto;
                        margin-top: 40px;
                        width: 40%;
                    }

                    .legende-item {
                        display: flex;
                        align-items: center;
                        margin: auto;
                    }

                    .kreis {
                        border-radius: 50%;
                        width: 20px;
                        aspect-ratio: 1/1;
                        margin-right: 5px;
                    }
                    </style>

                    <body>
                    <div class='background-blur'></div>
                    <section>
                        <h1 style='text-align: center; color: black'>Monatsverbrauch von Gas & Strom</h1>
                        <div class='grid-container'>
                    
                        %Hier Diagramme einfügen%
                        
                        </div>

                        <div class='legende'>
                        <div class='legende-item'>
                            <div class='kreis' style='background-color: #3AB795'></div>Gas in CBM</div>
                        <div class='legende-item'>
                            <div class='kreis' style='background-color: #FFCF56'></div>Strom in kWh</div>
                        </div>
                    </section>
                    </body>
                    </html>";
        }
    }
}

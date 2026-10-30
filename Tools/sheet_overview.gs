/**
 * Mirror Game: builds sheet 2 "Übersicht" in the results spreadsheet: key figures, breakdowns by species and
 * avatar gender, and a legend that explains every column of sheet 1.
 *
 * Setup: open the results sheet > Erweiterungen > Apps Script > "+" > Skript, name it "Uebersicht", paste this
 * file, save, pick setupOverview in the toolbar and press "Ausführen" (confirm the permission prompt once).
 * All numbers are formulas on sheet 1, so they update with every new row. Rerun only to rebuild the layout,
 * e.g. after changing SPECIES. The web app (doPost) does not need a new deployment.
 */

// config.json "species": which avatar s = 1-4 stands for. Keep in sync with the game.
const SPECIES = ["humanC", "goblin", "robot", "cow"];

const OVERVIEW_NAME = "Übersicht";
const COLOR_SECTION = "#2f3e5c";
const COLOR_HEADER = "#dde3ee";
const COLOR_BORDER = "#c3cad8";
const WIDTH = 8; // columns A-H

// Column of sheet 1, key, label, meaning. Order = order of the columns written by doPost.
const COLUMNS = [
  ["A", "timestamp", "Zeitpunkt", "Wann das Ergebnis im Sheet angekommen ist (am Ende des Spiels)."],
  ["B", "pid", "Teilnehmer-ID", "ResponseID aus Umfrage A. Verbindet Umfrage A, Spiel und Umfrage B. Beginnt sie mit „test-“, wurde das Spiel ohne Umfrage gestartet."],
  ["C", "g", "Geschlecht (Umfrage)", "Antwort aus Umfrage A, unverändert übernommen: m = männlich, w = weiblich, alles andere (z. B. x oder leer) = andere Antwort / keine Angabe."],
  ["D", "ag", "Avatar-Geschlecht", "Geschlecht des Avatars, der im Spiegel zu sehen war: m oder w. Bei g = m bzw. w identisch mit g, sonst zufällig 50:50 gewählt."],
  ["E", "s", "Spezies", "Avatar-Spezies 1–4, von Qualtrics gleichmäßig zugeteilt. " + SPECIES.map((id, i) => (i + 1) + " = " + id).join(", ") + "."],
  ["F", "r1", "Runde 1: behalten", "Die spielende Person teilt 100 CHF auf und gibt an, wie viel sie selbst behält (0–100 CHF in 5er-Schritten). Das Gegenüber nimmt an, wenn sie höchstens 90 CHF behält, sonst lehnt es ab und beide erhalten 0 CHF."],
  ["G", "r2", "Runde 2: Antwort", "Das Gegenüber teilt 100 CHF und bietet 50 CHF an (50:50). 1 = angenommen, 0 = abgelehnt (dann erhalten beide 0 CHF)."],
  ["H", "r3", "Runde 3: behalten", "Wie Runde 1: Betrag, den die spielende Person von 100 CHF selbst behält. Angenommen bei höchstens 90 CHF."],
  ["I", "r4", "Runde 4: Antwort", "Das Gegenüber behält 75 CHF und bietet 25 CHF an. 1 = angenommen, 0 = abgelehnt (dann erhalten beide 0 CHF)."],
  ["J", "t1", "Runde 1: Entscheidungszeit", "Millisekunden vom Erscheinen der Bedienelemente (nachdem der Text fertig angezeigt ist) bis zum Bestätigen. Enthält 0,4 s Eingabesperre."],
  ["K", "t2", "Runde 2: Entscheidungszeit", "Wie t1, bis zum Klick auf Annehmen oder Ablehnen."],
  ["L", "t3", "Runde 3: Entscheidungszeit", "Wie t1."],
  ["M", "t4", "Runde 4: Entscheidungszeit", "Wie t2."],
  ["N", "bal", "Endkontostand", "Summe der CHF, die die spielende Person in allen 4 Runden erhalten hat (0–255 CHF)."],
  ["O", "dev", "Gerät", "mobile = Smartphone oder Tablet, pc = Computer."],
  ["P", "dur", "Spieldauer", "Sekunden vom Spielstart bis zum Absenden der Ergebnisse."],
  ["Q", "test", "Testlauf", "1 = Testlauf (ohne Umfrage gestartet oder mit debug=1), 0 = echter Durchgang. Die Kennzahlen oben zählen nur echte Durchgänge."],
  ["R", "ver", "Spielversion", "Versionsnummer des Spiels (config.json)."],
];

function setupOverview() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const data = ss.getSheets()[0];
  const sh = overviewSheet(ss, data);
  sh.clear();
  sh.getRange(1, 1, sh.getMaxRows(), sh.getMaxColumns()).breakApart();
  sh.setHiddenGridlines(true);

  const D = "'" + data.getName().replace(/'/g, "''") + "'!";
  const col = c => D + c + "2:" + c;
  const real = col("Q") + ",0";
  const n = "COUNTIFS(" + real + ")";
  const avg = (c, extra) => '=IFERROR(AVERAGEIFS(' + col(c) + "," + real + (extra || "") + '),"–")';
  const accepted = (c, extra) => '=IFERROR(COUNTIFS(' + real + (extra || "") + "," + col(c) + ',"<=90")/COUNTIFS(' + real + (extra || "") + '),"–")';

  let row = 1;
  sh.getRange(row, 1, 1, WIDTH).merge().setValue("Mirror Game – Übersicht").setFontSize(18).setFontWeight("bold");
  sh.setRowHeight(row, 36);
  row++;
  sh.getRange(row, 1, 1, WIDTH).merge()
    .setValue("Wird automatisch aus „" + data.getName() + "“ berechnet. Kennzahlen und Aufteilungen zählen nur echte Durchgänge (test = 0). Erklärung aller Spalten unten in der Legende.")
    .setFontColor("#555555").setWrap(true);
  sh.setRowHeight(row, 36);
  row += 2;

  // key figures
  row = section(sh, row, "Kennzahlen (echte Durchgänge)");
  row = header(sh, row, ["Kennzahl", "Wert", "Erklärung"]);
  const figures = [
    ["Echte Durchgänge", "=" + n, "0", "Anzahl Zeilen mit test = 0."],
    ["Testläufe", "=COUNTIFS(" + col("Q") + ",1)", "0", "Anzahl Zeilen mit test = 1 (nicht in den Kennzahlen enthalten)."],
    ["Runde 1: Ø behalten", avg("F"), '0.0 "CHF"', "Durchschnittlicher Betrag, den die spielende Person von 100 CHF selbst behält."],
    ["Runde 1: vom Gegenüber angenommen", accepted("F"), "0%", "Anteil der Aufteilungen mit höchstens 90 CHF für sich selbst."],
    ["Runde 2: 50:50-Angebot angenommen", avg("G"), "0%", "Anteil, der das faire Angebot (50 CHF) angenommen hat."],
    ["Runde 3: Ø behalten", avg("H"), '0.0 "CHF"', "Wie Runde 1."],
    ["Runde 3: vom Gegenüber angenommen", accepted("H"), "0%", "Wie Runde 1."],
    ["Runde 4: 25-CHF-Angebot angenommen", avg("I"), "0%", "Anteil, der das unfaire Angebot (25 von 100 CHF) angenommen hat."],
    ["Runde 1: Ø Entscheidungszeit", avg("J").replace("),\"–\")", ")/1000,\"–\")"), '0.0 "s"', "Vom Erscheinen der Bedienelemente bis zum Bestätigen."],
    ["Runde 2: Ø Entscheidungszeit", avg("K").replace("),\"–\")", ")/1000,\"–\")"), '0.0 "s"', ""],
    ["Runde 3: Ø Entscheidungszeit", avg("L").replace("),\"–\")", ")/1000,\"–\")"), '0.0 "s"', ""],
    ["Runde 4: Ø Entscheidungszeit", avg("M").replace("),\"–\")", ")/1000,\"–\")"), '0.0 "s"', ""],
    ["Ø Endkontostand", avg("N"), '0.0 "CHF"', "Summe der erhaltenen CHF über alle 4 Runden (max. 255 CHF)."],
    ["Ø Spieldauer", avg("P").replace("),\"–\")", ")/60,\"–\")"), '0.0 "min"', "Vom Spielstart bis zum Absenden."],
    ["Anteil Smartphone/Tablet", '=IFERROR(COUNTIFS(' + real + "," + col("O") + ',"mobile")/' + n + ',"–")', "0%", "Rest: Computer."],
  ];
  const first = row;
  figures.forEach(([label, formula, format, note]) => {
    sh.getRange(row, 1).setValue(label);
    sh.getRange(row, 2).setFormula(formula).setNumberFormat(format).setHorizontalAlignment("right");
    sh.getRange(row, 3, 1, WIDTH - 2).merge().setValue(note).setFontColor("#555555");
    row++;
  });
  borders(sh, first - 1, row - first + 1, WIDTH);
  row++;

  // breakdowns
  const groupHeader = ["Gruppe", "Durchgänge", "Ø R1 behalten", "R1 angenommen", "R2 angenommen", "Ø R3 behalten", "R3 angenommen", "R4 angenommen"];
  const groupFormats = ["0", '0.0 "CHF"', "0%", "0%", '0.0 "CHF"', "0%", "0%"];
  const groupRow = (label, c, value) => {
    const extra = "," + col(c) + "," + value;
    sh.getRange(row, 1).setValue(label);
    const formulas = ["=COUNTIFS(" + real + extra + ")", avg("F", extra), accepted("F", extra), avg("G", extra),
                      avg("H", extra), accepted("H", extra), avg("I", extra)];
    formulas.forEach((f, i) => sh.getRange(row, 2 + i).setFormula(f).setNumberFormat(groupFormats[i]).setHorizontalAlignment("right"));
    row++;
  };

  row = section(sh, row, "Nach Spezies (s)");
  let start = row;
  row = header(sh, row, groupHeader);
  SPECIES.forEach((id, i) => groupRow((i + 1) + " – " + id, "E", i + 1));
  borders(sh, start, row - start, WIDTH);
  row++;

  row = section(sh, row, "Nach Avatar-Geschlecht (ag)");
  start = row;
  row = header(sh, row, groupHeader);
  groupRow("m – männlich", "D", '"m"');
  groupRow("w – weiblich", "D", '"w"');
  borders(sh, start, row - start, WIDTH);
  row++;

  // legend
  row = section(sh, row, "Legende: Spalten von „" + data.getName() + "“");
  start = row;
  row = header(sh, row, ["Bezeichnung", "Kürzel", "Spalte", "Bedeutung und Werte"]);
  COLUMNS.forEach(([letter, key, label, meaning]) => {
    sh.getRange(row, 1, 1, 3).setValues([[label, key, letter]]);
    sh.getRange(row, 2, 1, 2).setHorizontalAlignment("center").setFontFamily("Roboto Mono");
    sh.getRange(row, 4, 1, WIDTH - 3).merge().setValue(meaning).setWrap(true);
    sh.setRowHeight(row, meaning.length > 110 ? 54 : 36);
    row++;
  });
  borders(sh, start, row - start, WIDTH);

  sh.getRange(1, 1, row, WIDTH).setVerticalAlignment("middle");
  sh.setColumnWidth(1, 290);
  for (let c = 2; c <= WIDTH; c++) sh.setColumnWidth(c, 125);
  ss.toast("Übersicht erstellt.");
}

// Sheet 2: an existing "Übersicht", else an empty second sheet (renamed), else a new sheet in position 2.
function overviewSheet(ss, data) {
  const named = ss.getSheetByName(OVERVIEW_NAME);
  if (named) return named;
  const second = ss.getSheets()[1];
  if (second && second.getLastRow() === 0) return second.setName(OVERVIEW_NAME);
  return ss.insertSheet(OVERVIEW_NAME, 1);
}

function section(sh, row, title) {
  sh.getRange(row, 1, 1, WIDTH).merge().setValue(title)
    .setFontSize(12).setFontWeight("bold").setFontColor("#ffffff").setBackground(COLOR_SECTION);
  sh.setRowHeight(row, 30);
  return row + 1;
}

// Header cells; the last one spans the remaining columns.
function header(sh, row, labels) {
  labels.forEach((label, i) => {
    const span = i === labels.length - 1 ? WIDTH - i : 1;
    const range = sh.getRange(row, i + 1, 1, span);
    if (span > 1) range.merge();
    range.setValue(label).setFontWeight("bold").setBackground(COLOR_HEADER).setWrap(true);
    if (i > 0 && span === 1) range.setHorizontalAlignment("right");
  });
  sh.setRowHeight(row, 36);
  return row + 1;
}

function borders(sh, row, rows, cols) {
  sh.getRange(row, 1, rows, cols).setBorder(true, true, true, true, false, true, COLOR_BORDER, SpreadsheetApp.BorderStyle.SOLID);
}

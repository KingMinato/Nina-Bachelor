/**
 * Mirror Game: analysis of the game results for the thesis, inside the results spreadsheet.
 *
 * Creates three sheets next to the raw data (sheet 1, written by doPost):
 *   "Auswertung"         at a glance: colour-coded summary per species and avatar gender, then every participant
 *                        with avatar, gender and all four decisions in plain words
 *   "Statistik"          sample, descriptive statistics per group, group comparisons (tests + effect sizes), legend
 *   "Daten (bereinigt)"  one row per participant with readable column names and derived variables,
 *                        ready to download as CSV for SPSS / jamovi / R and to merge with Survey B via pid
 * Only real runs (test = 0) are used, and only the first run per pid.
 *
 * Everything is computed here, no sheet formulas (formulas break in sheets with a German locale).
 * Setup: Erweiterungen > Apps Script > "+" > Skript > paste this file > save > select updateAnalysis > Ausführen.
 * Afterwards the menu "Mirror Game > Auswertung aktualisieren" appears, and the analysis refreshes on every open.
 */

// Game parameters, keep in sync with config.json.
const SPECIES = ["humanC", "goblin", "robot", "cow"]; // "species": s = 1-4
const SPECIES_NAMES = { humanC: "Mensch (C)", humanI: "Mensch (I)", goblin: "Goblin", robot: "Roboter", skeleton: "Skelett",
  slime: "Schleim", mushroom: "Pilz", cow: "Kuh" };
const MONEY = 100;          // moneyPerRound
const NPC_ACCEPT_MAX = 90;  // npcAcceptMaxPlayerKeeps: the NPC accepts if the player keeps at most this
const R2_OFFER = 50;        // MONEY - round2NpcKeeps
const R4_OFFER = 25;        // MONEY - round4NpcKeeps

const OVERVIEW_NAME = "Auswertung";
const STATS_NAME = "Statistik";
const CLEAN_NAME = "Daten (bereinigt)";
const WIDTH = 9; // columns A-I of the analysis sheet
const COLOR_SECTION = "#2f3e5c";
const COLOR_HEADER = "#dde3ee";
const COLOR_SUB = "#f1f3f8";
const COLOR_BORDER = "#c3cad8";
const COLOR_GOOD = [183, 225, 205], COLOR_MID = [252, 232, 178], COLOR_BAD = [244, 199, 195];

function onOpen() {
  SpreadsheetApp.getUi().createMenu("Mirror Game").addItem("Auswertung aktualisieren", "updateAnalysis").addToUi();
  try {
    updateAnalysis();
  } catch (e) {
    console.warn(e);
  }
}

function updateAnalysis() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const raw = rawSheet(ss);
  const data = readRuns(raw);
  const comma = String(ss.getSpreadsheetLocale()).startsWith("de");
  writeOverview(sheetNamed(ss, OVERVIEW_NAME, "Übersicht"), raw.getName(), data);
  writeAnalysis(sheetNamed(ss, STATS_NAME), raw.getName(), data, comma);
  writeClean(sheetNamed(ss, CLEAN_NAME), data.runs);
  ss.toast("Auswertung aktualisiert (" + data.runs.length + " Durchgänge).");
}

// ------------------------------------------------------------ data

function rawSheet(ss) {
  return ss.getSheets().find(s => s.getRange(1, 1).getValue() === "timestamp") || ss.getSheets()[0];
}

// Sheet by name; an older sheet named `old` is renamed; otherwise a new sheet at the end.
function sheetNamed(ss, name, old) {
  let sh = ss.getSheetByName(name);
  if (!sh && old) sh = ss.getSheetByName(old);
  if (!sh) sh = ss.insertSheet(name, ss.getSheets().length);
  if (sh.getName() !== name) sh.setName(name);
  return sh;
}

function readRuns(sheet) {
  const values = sheet.getDataRange().getValues();
  const head = values[0].map(String);
  const col = key => head.indexOf(key);
  const num = v => (v === "" || v === null ? NaN : Number(v));
  const runs = [];
  const seen = new Set();
  let tests = 0, duplicates = 0, invalid = 0;
  for (let i = 1; i < values.length; i++) {
    const row = values[i];
    const get = key => (col(key) < 0 ? "" : row[col(key)]);
    if (num(get("test")) === 1) { tests++; continue; }
    const keep1 = num(get("r1")), keep3 = num(get("r3"));
    const acc2 = num(get("r2")), acc4 = num(get("r4"));
    if (![keep1, keep3, acc2, acc4].every(isFinite)) { invalid++; continue; }
    const pid = String(get("pid"));
    if (seen.has(pid)) { duplicates++; continue; }
    seen.add(pid);
    const s = num(get("s"));
    const g = String(get("g")).trim().toLowerCase();
    runs.push({
      time: get("timestamp"), pid, g, ag: String(get("ag")), s,
      species: SPECIES[s - 1] || "?",
      gender: g === "m" ? "m" : g === "w" ? "w" : "andere",
      dev: String(get("dev")),
      keep1, keep3, acc2, acc4,
      offer1: MONEY - keep1, offer3: MONEY - keep3,
      npc1: keep1 <= NPC_ACCEPT_MAX ? 1 : 0, npc3: keep3 <= NPC_ACCEPT_MAX ? 1 : 0,
      offerMean: MONEY - (keep1 + keep3) / 2,
      times: ["t1", "t2", "t3", "t4"].map(k => num(get(k)) / 1000),
      bal: num(get("bal")), dur: num(get("dur")) / 60, ver: String(get("ver")),
    });
  }
  return { runs, tests, duplicates, invalid };
}

// ------------------------------------------------------------ clean data sheet

function writeClean(sh, runs) {
  const header = ["Teilnehmer-ID (pid)", "Datum", "Geschlecht Umfrage (g)", "Avatar-Geschlecht (ag)", "Spezies-Nr (s)", "Spezies",
    "Gerät", "R1 behalten", "R1 Angebot", "R1 vom Gegenüber angenommen", "R2 angenommen (Angebot 50)",
    "R3 behalten", "R3 Angebot", "R3 vom Gegenüber angenommen", "R4 angenommen (Angebot 25)", "Ø Angebot R1+R3",
    "R1 Zeit (s)", "R2 Zeit (s)", "R3 Zeit (s)", "R4 Zeit (s)", "Endkontostand (CHF)", "Spieldauer (min)", "Version"];
  const rows = runs.map(r => [r.pid, r.time, r.g, r.ag, r.s, r.species, r.dev, r.keep1, r.offer1, r.npc1, r.acc2,
    r.keep3, r.offer3, r.npc3, r.acc4, r.offerMean, ...r.times, r.bal, r.dur, r.ver]);
  sh.clear();
  sh.getRange(1, 1, 1, header.length).setValues([header]).setFontWeight("bold").setBackground(COLOR_HEADER).setWrap(true);
  sh.setFrozenRows(1);
  if (rows.length) {
    sh.getRange(2, 1, rows.length, header.length).setValues(rows);
    sh.getRange(2, 2, rows.length, 1).setNumberFormat("dd.MM.yyyy HH:mm");
    sh.getRange(2, 16, rows.length, 1).setNumberFormat("0.0");
    sh.getRange(2, 17, rows.length, 4).setNumberFormat("0.00");
    sh.getRange(2, 22, rows.length, 1).setNumberFormat("0.0");
  }
  sh.setRowHeight(1, 48);
  sh.setColumnWidth(1, 180);
  for (let c = 2; c <= header.length; c++) sh.setColumnWidth(c, 105);
}

// ------------------------------------------------------------ overview sheet

const speciesName = s => SPECIES_NAMES[SPECIES[s - 1]] || SPECIES[s - 1] || "?";
const genderName = g => (g === "m" ? "männlich" : g === "w" ? "weiblich" : "andere / keine Angabe");

// Red (0) over yellow to green (1).
function heat(t) {
  t = Math.max(0, Math.min(1, t));
  const [a, b, k] = t < 0.5 ? [COLOR_BAD, COLOR_MID, t * 2] : [COLOR_MID, COLOR_GOOD, t * 2 - 1];
  return "#" + a.map((x, i) => Math.round(x + (b[i] - x) * k).toString(16).padStart(2, "0")).join("");
}
const offerColor = offer => heat(Math.min(offer, 50) / 50); // 50:50 and more = green, nothing = red

function writeOverview(sh, rawName, data) {
  const runs = data.runs;
  const w = writer(10, [190, 120, 150, 150, 150, 150, 150, 110, 100, 110]);
  w.title("Mirror Game – Auswertung");
  w.note(runs.length + " Personen (nur echte Durchgänge aus „" + rawName + "“; " + data.tests + " Testläufe und " +
    data.duplicates + " doppelte Durchgänge ausgeblendet). Stand: " +
    Utilities.formatDate(new Date(), Session.getScriptTimeZone(), "dd.MM.yyyy HH:mm") +
    ". Grün = fair bzw. angenommen, rot = unfair bzw. abgelehnt. Signifikanztests im Blatt „" + STATS_NAME + "“.");
  w.blank();
  if (!runs.length) {
    w.note("Noch keine echten Durchgänge vorhanden.");
    w.flush(sh);
    return;
  }

  // summary per species and avatar gender
  w.section("Auf einen Blick: Spezies × Avatar-Geschlecht");
  w.note("Runde 1 und 3: Die Person teilt 100 CHF auf; angegeben ist, wie viel sie dem Gegenüber im Schnitt abgibt (fair = 50 CHF). " +
    "Runde 2: Das Gegenüber bietet fair " + R2_OFFER + " CHF an. Runde 4: Das Gegenüber bietet unfair " + R4_OFFER +
    " CHF an. Angegeben ist jeweils, wie viele Personen annehmen.");
  w.gridHeader(["Spezies", "Avatar", "Personen", "Runde 1: Ø abgegeben", "Runde 3: Ø abgegeben",
    "Runde 2: 50:50 angenommen", "Runde 4: 25 CHF angenommen", "Ø Kontostand"]);
  const summary = (label, gender, group, bold) => {
    if (!group.length) {
      w.grid([{ v: label, b: bold }, { v: gender, b: bold }, { v: 0, f: "0", b: bold }, "–", "–", "–", "–", "–"]);
      return;
    }
    const mean = f => Stats.mean(group.map(f));
    const rate = f => group.filter(f).length / group.length;
    const o1 = mean(r => r.offer1), o3 = mean(r => r.offer3), a2 = rate(r => r.acc2 === 1), a4 = rate(r => r.acc4 === 1);
    w.grid([{ v: label, b: bold }, { v: gender, b: bold }, { v: group.length, f: "0", b: bold },
      { v: o1, f: '0.0 "CHF"', bg: offerColor(o1), b: bold }, { v: o3, f: '0.0 "CHF"', bg: offerColor(o3), b: bold },
      { v: a2, f: "0%", bg: heat(a2), b: bold }, { v: a4, f: "0%", bg: heat(a4), b: bold },
      { v: mean(r => r.bal), f: '0 "CHF"', b: bold }]);
  };
  SPECIES.forEach((id, i) => {
    const group = runs.filter(r => r.s === i + 1);
    summary(speciesName(i + 1), "männlich", group.filter(r => r.ag === "m"));
    summary("", "weiblich", group.filter(r => r.ag === "w"));
    summary("", "zusammen", group, true);
  });
  summary("Alle", "männlich", runs.filter(r => r.ag === "m"));
  summary("", "weiblich", runs.filter(r => r.ag === "w"));
  summary("", "zusammen", runs, true);
  w.blank();

  // every participant
  w.section("Alle Personen");
  w.note("Sortiert nach Spezies und Avatar-Geschlecht. „gibt“ = Betrag, den die Person von 100 CHF dem Gegenüber abgibt; " +
    "behält sie mehr als " + NPC_ACCEPT_MAX + " CHF, lehnt das Gegenüber ab und beide bekommen nichts.");
  const header = ["Teilnehmer-ID", "Avatar", "Geschlecht (Umfrage)", "Runde 1: teilt", "Runde 2: 50:50-Angebot",
    "Runde 3: teilt", "Runde 4: 25-CHF-Angebot", "Kontostand", "Spieldauer", "Gerät"];
  const split = (offer, accepted) => accepted
    ? { v: "gibt " + offer + " CHF", bg: offerColor(offer) }
    : { v: "gibt " + offer + " CHF → abgelehnt", bg: offerColor(offer), fc: "#a50e0e" };
  const answer = accepted => ({ v: accepted ? "angenommen" : "abgelehnt", bg: heat(accepted ? 1 : 0) });
  const person = r => w.grid([r.pid, speciesName(r.s) + ", " + (r.ag === "m" ? "männlich" : "weiblich"), genderName(r.gender),
    split(r.offer1, r.npc1), answer(r.acc2 === 1), split(r.offer3, r.npc3), answer(r.acc4 === 1),
    { v: r.bal, f: '0 "CHF"' }, { v: r.dur, f: '0.0 "min"' }, r.dev === "mobile" ? "Handy/Tablet" : "Computer"]);
  SPECIES.forEach((id, i) => {
    const group = runs.filter(r => r.s === i + 1).sort((a, b) => a.ag.localeCompare(b.ag));
    w.sub(speciesName(i + 1) + " (" + group.length + (group.length === 1 ? " Person)" : " Personen)"));
    if (!group.length) return;
    w.gridHeader(header);
    group.forEach(person);
  });
  const other = runs.filter(r => !(r.s >= 1 && r.s <= SPECIES.length));
  if (other.length) {
    w.sub("Unbekannte Spezies (" + other.length + ")");
    w.gridHeader(header);
    other.forEach(person);
  }
  w.flush(sh);
}

// ------------------------------------------------------------ statistics sheet

function writeAnalysis(sh, rawName, data, comma) {
  const runs = data.runs;
  const w = writer(WIDTH, [250].concat(new Array(WIDTH - 1).fill(120)));
  const dec = (x, d) => (comma ? x.toFixed(d).replace(".", ",") : x.toFixed(d));
  const fmtP = p => (p < 0.001 ? "< " + dec(0.001, 3) : "= " + dec(p, 3)).replace(" 0", " ");

  w.title("Mirror Game – Statistik");
  w.note("Stand: " + Utilities.formatDate(new Date(), Session.getScriptTimeZone(), "dd.MM.yyyy HH:mm") +
    ". Grundlage: Blatt „" + rawName + "“, nur echte Durchgänge (test = 0), pro Teilnehmer-ID nur der erste Durchgang. " +
    "Ausgeschlossen: " + data.tests + " Testläufe, " + data.duplicates + " doppelte Durchgänge" +
    (data.invalid ? ", " + data.invalid + " unvollständige Zeilen" : "") +
    ". Aktualisieren: Menü „Mirror Game > Auswertung aktualisieren“ (passiert auch beim Öffnen). " +
    "Die Daten pro Person mit verständlichen Spaltennamen stehen im Blatt „" + CLEAN_NAME + "“ (Datei > Herunterladen > CSV für SPSS, jamovi oder R; Zusammenführen mit Umfrage B über die Teilnehmer-ID).");
  w.note("Wichtig: Wer in Umfrage A „männlich“ oder „weiblich“ angibt, bekommt immer einen Avatar desselben Geschlechts. " +
    "Unterschiede nach Avatar-Geschlecht lassen sich deshalb nicht vom Geschlecht der Person trennen (nur „andere/keine Angabe“ bekommt ein zufälliges Avatar-Geschlecht). " +
    "Die Tests unten sind gängige Standardverfahren für einen ersten Überblick; welche Hypothesen und Tests in der Arbeit berichtet werden, mit der Betreuung abstimmen. " +
    "Bei kleinen Gruppen (unter etwa 20 Personen pro Gruppe) sind p-Werte nur eingeschränkt aussagekräftig.");
  w.blank();

  if (!runs.length) {
    w.note("Noch keine echten Durchgänge vorhanden.");
    w.flush(sh);
    return;
  }

  const groups = [["Alle", null, () => true]];
  SPECIES.forEach((id, i) => groups.push([(i + 1) + " – " + id, "Spezies (s)", r => r.s === i + 1]));
  groups.push(["m – männlich", "Avatar-Geschlecht (ag)", r => r.ag === "m"]);
  groups.push(["w – weiblich", "Avatar-Geschlecht (ag)", r => r.ag === "w"]);
  groups.push(["männlich", "Geschlecht laut Umfrage A (g)", r => r.gender === "m"]);
  groups.push(["weiblich", "Geschlecht laut Umfrage A (g)", r => r.gender === "w"]);
  groups.push(["andere / keine Angabe", "Geschlecht laut Umfrage A (g)", r => r.gender === "andere"]);
  groups.push(["Computer", "Gerät (dev)", r => r.dev === "pc"]);
  groups.push(["Smartphone / Tablet", "Gerät (dev)", r => r.dev === "mobile"]);

  // Writes one row per group under a header, with a subheader row whenever the grouping changes.
  const table = (header, cells) => {
    w.header(header);
    let last = null;
    groups.forEach(([label, kind, filter]) => {
      if (kind !== last && kind) w.sub(kind);
      last = kind;
      const g = runs.filter(filter);
      w.row([label, num(g.length, "0")].concat(g.length ? cells(g) : []));
    });
    w.blank();
  };
  const share = (g, f) => num(g.filter(f).length / g.length, "0%");
  const mean = (g, f) => num(Stats.mean(g.map(f)), "0.0");
  const sd = (g, f) => num(Stats.sd(g.map(f)), "0.0");
  const median = (g, f) => num(Stats.median(g.map(f)), "0.0");

  // 1 sample
  w.section("1  Stichprobe");
  table(["Gruppe", "n", "Anteil an allen", "Ø Spieldauer (min)", "Anteil Smartphone/Tablet"],
    g => [num(g.length / runs.length, "0%"), mean(g, r => r.dur), share(g, r => r.dev === "mobile")]);

  // 2 proposer
  w.section("2  Runde 1 und 3: Die Person teilt 100 CHF auf");
  w.note("Angebot = Betrag, den die Person dem Gegenüber abgibt (100 − behalten), in CHF. Fair = 50:50. " +
    "Das Gegenüber lehnt ab, wenn die Person mehr als " + NPC_ACCEPT_MAX + " CHF behält; dann erhalten beide nichts. " +
    "SD = Standardabweichung. Runde 3 zeigt, ob sich das Verhalten nach den Erfahrungen aus Runde 1 und 2 ändert.");
  table(["Gruppe", "n", "R1 Ø Angebot", "R1 SD", "R1 Median", "R3 Ø Angebot", "R3 SD", "R3 Median", "Ø Angebot R1+R3"],
    g => [mean(g, r => r.offer1), sd(g, r => r.offer1), median(g, r => r.offer1),
          mean(g, r => r.offer3), sd(g, r => r.offer3), median(g, r => r.offer3), mean(g, r => r.offerMean)]);
  table(["Gruppe", "n", "R1 faire Angebote (50)", "R1 großzügig (über 50)", "R1 vom Gegenüber abgelehnt",
         "R3 faire Angebote (50)", "R3 großzügig (über 50)", "R3 vom Gegenüber abgelehnt"],
    g => [share(g, r => r.offer1 === 50), share(g, r => r.offer1 > 50), share(g, r => r.npc1 === 0),
          share(g, r => r.offer3 === 50), share(g, r => r.offer3 > 50), share(g, r => r.npc3 === 0)]);

  // 3 responder
  w.section("3  Runde 2 und 4: Die Person nimmt ein Angebot an oder lehnt ab");
  w.note("Runde 2: faires Angebot (" + R2_OFFER + " von 100 CHF). Runde 4: unfaires Angebot (" + R4_OFFER + " von 100 CHF). " +
    "Wer ablehnt, verzichtet selbst auf Geld, damit auch das Gegenüber nichts bekommt (Bestrafung von Unfairness).");
  table(["Gruppe", "n", "R2 angenommen (Anzahl)", "R2 Annahmequote", "R4 angenommen (Anzahl)", "R4 Annahmequote", "R4 Ablehnungsquote"],
    g => [num(g.filter(r => r.acc2 === 1).length, "0"), share(g, r => r.acc2 === 1),
          num(g.filter(r => r.acc4 === 1).length, "0"), share(g, r => r.acc4 === 1), share(g, r => r.acc4 === 0)]);

  // 4 balance and decision times
  w.section("4  Endkontostand und Entscheidungszeiten");
  w.note("Endkontostand: Summe der erhaltenen CHF aus allen 4 Runden (höchstens 255 CHF). " +
    "Entscheidungszeit: Sekunden vom Erscheinen der Bedienelemente (nach dem fertig angezeigten Text) bis zur Entscheidung, inklusive 0,4 s Eingabesperre. " +
    "Für Zeiten wird der Median angegeben, weil einzelne sehr lange Pausen den Mittelwert stark verzerren.");
  table(["Gruppe", "n", "Ø Endkontostand", "SD", "Median Zeit R1 (s)", "Median Zeit R2 (s)", "Median Zeit R3 (s)", "Median Zeit R4 (s)"],
    g => [mean(g, r => r.bal), sd(g, r => r.bal)].concat([0, 1, 2, 3].map(i => num(Stats.median(g.map(r => r.times[i])), "0.0"))));

  // 5 tests
  w.section("5  Gruppenvergleiche (Signifikanztests)");
  w.note("Prüft, ob sich die Gruppen stärker unterscheiden, als durch Zufall zu erwarten wäre. p < .05 gilt üblicherweise als statistisch signifikant. " +
    "ANOVA bzw. t-Test vergleichen Mittelwerte, Kruskal-Wallis bzw. Mann-Whitney sind die robusten Varianten ohne Normalverteilungsannahme (bei Geldbeträgen im Ultimatumspiel oft passender). " +
    "χ²-Test vergleicht Anteile (angenommen/abgelehnt). Effektstärken: η² klein ab .01, mittel ab .06, groß ab .14; Cohen's d klein ab 0.2, mittel ab 0.5, groß ab 0.8; Cramér's V klein ab .10, mittel ab .30, groß ab .50.");
  const testHeader = ["Abhängige Variable", "Test", "Ergebnis", "p", "Effektstärke", "Hinweis"];
  const metric = [["Angebot R1", r => r.offer1], ["Angebot R3", r => r.offer3], ["Ø Angebot R1+R3", r => r.offerMean], ["Endkontostand", r => r.bal]];
  const binary = [["Annahme R2 (faires Angebot)", r => r.acc2], ["Annahme R4 (unfaires Angebot)", r => r.acc4]];
  const p = x => (isFinite(x) ? "p " + fmtP(x) : "–");
  const none = reason => ["–", "–", "–", reason];

  const compare = (title, sets) => {
    w.sub(title + ": " + sets.map(s => s[0] + " (n = " + s[1].length + ")").join(", "));
    w.header(testHeader);
    const used = sets.filter(s => s[1].length > 0);
    metric.forEach(([label, f]) => {
      const values = used.map(s => s[1].map(f));
      if (used.length === 2) {
        const t = Stats.welch(values[0], values[1]);
        w.row([label, "t-Test (Welch)"].concat(isFinite(t.p)
          ? ["t(" + dec(t.df, 1) + ") = " + dec(t.t, 2), p(t.p), "d = " + dec(t.d, 2), ""] : none("zu wenige Daten oder keine Streuung")));
        const u = Stats.mannWhitney(values[0], values[1]);
        w.row([label, "Mann-Whitney-U"].concat(isFinite(u.p)
          ? ["U = " + dec(u.U, 1) + ", z = " + dec(u.z, 2), p(u.p), "–", "robust"] : none("zu wenige Daten oder keine Streuung")));
      } else {
        const a = Stats.anova(values);
        w.row([label, "einfaktorielle ANOVA"].concat(isFinite(a.p)
          ? ["F(" + a.df1 + ", " + a.df2 + ") = " + dec(a.F, 2), p(a.p), "η² = " + dec(a.eta2, 2).replace(/^(-?)0/, "$1"), ""] : none("zu wenige Daten oder keine Streuung")));
        const k = Stats.kruskal(values);
        w.row([label, "Kruskal-Wallis"].concat(isFinite(k.p)
          ? ["H(" + k.df + ") = " + dec(k.H, 2), p(k.p), "–", "robust"] : none("zu wenige Daten oder keine Streuung")));
      }
    });
    binary.forEach(([label, f]) => {
      const c = Stats.chi2(used.map(s => { const yes = s[1].filter(r => f(r) === 1).length; return [yes, s[1].length - yes]; }));
      w.row([label, "χ²-Test"].concat(isFinite(c.p)
        ? ["χ²(" + c.df + ", N = " + c.N + ") = " + dec(c.chi2, 2), p(c.p), "V = " + dec(c.V, 2).replace(/^0/, ""),
           c.lowExpected ? "erwartete Häufigkeiten unter 5: nur eingeschränkt gültig (exakter Test nach Fisher besser)" : ""]
        : none("alle haben gleich entschieden oder zu wenige Daten")));
    });
    w.blank();
  };
  const bySpecies = SPECIES.map((id, i) => [(i + 1) + " " + id, runs.filter(r => r.s === i + 1)]);
  compare("Spezies", bySpecies);
  compare("Avatar-Geschlecht", [["m", runs.filter(r => r.ag === "m")], ["w", runs.filter(r => r.ag === "w")]]);
  compare("Geschlecht laut Umfrage A", [["männlich", runs.filter(r => r.gender === "m")], ["weiblich", runs.filter(r => r.gender === "w")]]);

  w.sub("Veränderung von Runde 1 zu Runde 3 (dieselben Personen, n = " + runs.length + ")");
  w.header(testHeader);
  const pt = Stats.pairedT(runs.map(r => r.offer1), runs.map(r => r.offer3));
  w.row(["Angebot R1 vs. R3", "t-Test für verbundene Stichproben"].concat(isFinite(pt.p)
    ? ["t(" + pt.df + ") = " + dec(pt.t, 2), p(pt.p), "dz = " + dec(pt.dz, 2),
       "Ø Veränderung " + dec(pt.diff, 1) + " CHF (positiv: in Runde 3 mehr abgegeben)"] : none("zu wenige Daten oder keine Veränderung")));
  const rr = Stats.pearson(runs.map(r => r.offer1), runs.map(r => r.offer3));
  w.row(["Angebot R1 und R3", "Korrelation (Pearson)"].concat(isFinite(rr.p)
    ? ["r = " + dec(rr.r, 2).replace(/^(-?)0/, "$1"), p(rr.p), "–", "wie stabil das Verhalten einer Person ist"] : none("zu wenige Daten oder keine Streuung")));
  w.blank();

  // 6 legend
  w.section("6  Legende der Rohdaten (Blatt „" + rawName + "“)");
  w.header(["Kürzel", "Bedeutung und Werte"]);
  LEGEND.forEach(([key, text]) => w.legend(key, text));

  w.flush(sh);
}

const LEGEND = [
  ["timestamp", "Zeitpunkt, an dem das Ergebnis am Ende des Spiels im Sheet angekommen ist."],
  ["pid", "Teilnehmer-ID: ResponseID aus Umfrage A. Verbindet Umfrage A, Spiel und Umfrage B. Beginnt sie mit „test-“, wurde das Spiel ohne Umfrage gestartet."],
  ["g", "Geschlecht laut Umfrage A, unverändert: m = männlich, w = weiblich, alles andere (z. B. x oder leer) = andere Antwort oder keine Angabe."],
  ["ag", "Geschlecht des Avatars im Spiegel: m oder w. Bei g = m bzw. w gleich g, sonst zufällig 50:50."],
  ["s", "Spezies des Avatars 1–4, von Qualtrics gleichmäßig zugeteilt: " + SPECIES.map((id, i) => (i + 1) + " = " + id).join(", ") + "."],
  ["r1", "Runde 1: Betrag (0–100 CHF in 5er-Schritten), den die Person von 100 CHF selbst behält. Das Gegenüber nimmt an, wenn er höchstens " + NPC_ACCEPT_MAX + " CHF beträgt, sonst erhalten beide 0 CHF."],
  ["r2", "Runde 2: Das Gegenüber bietet " + R2_OFFER + " von 100 CHF an. 1 = angenommen, 0 = abgelehnt (beide erhalten 0 CHF)."],
  ["r3", "Runde 3: wie Runde 1."],
  ["r4", "Runde 4: Das Gegenüber bietet " + R4_OFFER + " von 100 CHF an und behält " + (MONEY - R4_OFFER) + ". 1 = angenommen, 0 = abgelehnt (beide erhalten 0 CHF)."],
  ["t1 – t4", "Entscheidungszeit pro Runde in Millisekunden: vom Erscheinen der Bedienelemente (nach dem fertig angezeigten Text) bis zum Bestätigen bzw. Annehmen/Ablehnen, inklusive 0,4 s Eingabesperre."],
  ["bal", "Endkontostand: Summe der erhaltenen CHF über alle 4 Runden (0–255 CHF)."],
  ["dev", "Gerät: mobile = Smartphone oder Tablet, pc = Computer."],
  ["dur", "Spieldauer in Sekunden vom Spielstart bis zum Absenden."],
  ["test", "1 = Testlauf (ohne Umfrage gestartet oder mit debug=1), 0 = echter Durchgang. Testläufe sind in der Auswertung nicht enthalten."],
  ["ver", "Versionsnummer des Spiels."],
];

const num = (x, format) => (isFinite(x) ? { v: x, f: format } : "–");

// Collects rows and their styles, then writes everything in a few calls.
// A cell is a value or { v, f (number format), bg (background), fc (font colour), b (bold) }.
function writer(width, columnWidths) {
  const rows = [], formats = [], backgrounds = [], colors = [], weights = [], styles = [];
  const add = (cells, style) => {
    const values = [], fmt = [], bg = [], fc = [], fw = [];
    for (let i = 0; i < width; i++) {
      const c = i < cells.length ? cells[i] : "";
      const o = c !== null && typeof c === "object" ? c : { v: c };
      values.push(o.v);
      fmt.push(o.f || "@");
      bg.push(o.bg || null);
      fc.push(o.fc || null);
      fw.push(o.b ? "bold" : "normal");
    }
    rows.push(values);
    formats.push(fmt);
    backgrounds.push(bg);
    colors.push(fc);
    weights.push(fw);
    styles.push(style);
  };
  return {
    title: t => add([t], "title"),
    note: t => add([t], "note"),
    section: t => add([t], "section"),
    sub: t => add([t], "sub"),
    header: cells => add(cells, "header"),
    row: cells => add(cells, "row"),
    gridHeader: cells => add(cells, "gridHeader"),
    grid: cells => add(cells, "grid"),
    legend: (key, text) => add([key, text], "legend"),
    blank: () => add([], "blank"),
    flush(sh) {
      sh.clear();
      sh.getRange(1, 1, sh.getMaxRows(), sh.getMaxColumns()).breakApart();
      sh.setHiddenGridlines(true);
      sh.setFrozenRows(0);
      const all = sh.getRange(1, 1, rows.length, width);
      all.setNumberFormats(formats).setValues(rows).setBackgrounds(backgrounds).setFontColors(colors).setFontWeights(weights)
        .setVerticalAlignment("middle").setFontSize(10);
      const border = range => range.setBorder(null, null, true, null, null, null, COLOR_BORDER, SpreadsheetApp.BorderStyle.SOLID);
      styles.forEach((style, i) => {
        const r = i + 1;
        const line = sh.getRange(r, 1, 1, width);
        if (style === "title") {
          line.merge().setFontSize(18).setFontWeight("bold");
          sh.setRowHeight(r, 38);
        } else if (style === "note") {
          line.merge().setWrap(true).setFontColor("#444444");
          sh.setRowHeight(r, 21 * Math.ceil(String(rows[i][0]).length / (width * 16)) + 8);
        } else if (style === "section") {
          line.merge().setFontSize(12).setFontWeight("bold").setFontColor("#ffffff").setBackground(COLOR_SECTION);
          sh.setRowHeight(r, 30);
        } else if (style === "sub") {
          line.merge().setFontWeight("bold").setBackground(COLOR_SUB);
          sh.setRowHeight(r, 26);
        } else if (style === "header") {
          line.setFontWeight("bold").setBackground(COLOR_HEADER).setWrap(true);
          sh.getRange(r, 2, 1, width - 1).setHorizontalAlignment("right");
          if (rows[i][1] === "Bedeutung und Werte" || rows[i][1] === "Test") sh.getRange(r, 2, 1, width - 1).setHorizontalAlignment("left");
          if (rows[i][1] === "Bedeutung und Werte") sh.getRange(r, 2, 1, width - 1).merge();
          if (rows[i][1] === "Test") sh.getRange(r, 6, 1, width - 5).merge();
          sh.setRowHeight(r, 36);
        } else if (style === "row") {
          border(line);
          const tests = rows[i][1] !== "" && typeof rows[i][1] === "string" && rows[i][1] !== "–";
          sh.getRange(r, 2, 1, width - 1).setHorizontalAlignment(tests ? "left" : "right");
          if (tests) sh.getRange(r, 6, 1, width - 5).merge().setWrap(true);
        } else if (style === "gridHeader") {
          line.setFontWeight("bold").setBackground(COLOR_HEADER).setWrap(true).setHorizontalAlignment("center");
          sh.getRange(r, 1).setHorizontalAlignment("left");
          sh.setRowHeight(r, 40);
        } else if (style === "grid") {
          border(line.setHorizontalAlignment("center"));
          sh.getRange(r, 1).setHorizontalAlignment("left");
          sh.setRowHeight(r, 24);
        } else if (style === "legend") {
          sh.getRange(r, 1).setFontFamily("Roboto Mono");
          sh.getRange(r, 2, 1, width - 1).merge().setWrap(true);
          border(line);
          sh.setRowHeight(r, String(rows[i][1]).length > 130 ? 40 : 24);
        }
      });
      columnWidths.forEach((w, c) => sh.setColumnWidth(c + 1, w));
    },
  };
}

// ------------------------------------------------------------ statistics

const Stats = {
  sum: a => a.reduce((s, x) => s + x, 0),
  mean: a => (a.length ? Stats.sum(a) / a.length : NaN),
  variance(a) {
    if (a.length < 2) return NaN;
    const m = Stats.mean(a);
    return Stats.sum(a.map(x => (x - m) * (x - m))) / (a.length - 1);
  },
  sd: a => Math.sqrt(Stats.variance(a)),
  median(a) {
    if (!a.length) return NaN;
    const s = a.slice().sort((x, y) => x - y), h = s.length >> 1;
    return s.length % 2 ? s[h] : (s[h - 1] + s[h]) / 2;
  },

  logGamma(x) {
    const c = [0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059,
      12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7];
    if (x < 0.5) return Math.log(Math.PI / Math.sin(Math.PI * x)) - Stats.logGamma(1 - x);
    x -= 1;
    let a = c[0];
    const t = x + 7.5;
    for (let i = 1; i < 9; i++) a += c[i] / (x + i);
    return 0.5 * Math.log(2 * Math.PI) + (x + 0.5) * Math.log(t) - t + Math.log(a);
  },
  // Continued fraction of the incomplete beta function (Numerical Recipes betacf).
  betacf(x, a, b) {
    const tiny = 1e-300;
    const qab = a + b, qap = a + 1, qam = a - 1;
    let c = 1, d = 1 - qab * x / qap;
    if (Math.abs(d) < tiny) d = tiny;
    d = 1 / d;
    let h = d;
    for (let m = 1; m <= 300; m++) {
      const m2 = 2 * m;
      let aa = m * (b - m) * x / ((qam + m2) * (a + m2));
      d = 1 + aa * d; if (Math.abs(d) < tiny) d = tiny;
      c = 1 + aa / c; if (Math.abs(c) < tiny) c = tiny;
      d = 1 / d; h *= d * c;
      aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
      d = 1 + aa * d; if (Math.abs(d) < tiny) d = tiny;
      c = 1 + aa / c; if (Math.abs(c) < tiny) c = tiny;
      d = 1 / d;
      const del = d * c;
      h *= del;
      if (Math.abs(del - 1) < 3e-14) break;
    }
    return h;
  },
  // Regularized incomplete beta function I_x(a, b).
  ibeta(x, a, b) {
    if (x <= 0) return 0;
    if (x >= 1) return 1;
    const bt = Math.exp(Stats.logGamma(a + b) - Stats.logGamma(a) - Stats.logGamma(b) + a * Math.log(x) + b * Math.log(1 - x));
    return x < (a + 1) / (a + b + 2) ? bt * Stats.betacf(x, a, b) / a : 1 - bt * Stats.betacf(1 - x, b, a) / b;
  },
  // Regularized upper incomplete gamma function Q(a, x).
  gammaQ(a, x) {
    if (x <= 0) return 1;
    const gln = Stats.logGamma(a);
    if (x < a + 1) {
      let ap = a, sum = 1 / a, del = sum;
      for (let n = 0; n < 1000; n++) {
        ap++; del *= x / ap; sum += del;
        if (Math.abs(del) < Math.abs(sum) * 3e-14) break;
      }
      return 1 - sum * Math.exp(-x + a * Math.log(x) - gln);
    }
    const tiny = 1e-300;
    let b = x + 1 - a, c = 1 / tiny, d = 1 / b, h = d;
    for (let i = 1; i < 1000; i++) {
      const an = -i * (i - a);
      b += 2;
      d = an * d + b; if (Math.abs(d) < tiny) d = tiny;
      c = b + an / c; if (Math.abs(c) < tiny) c = tiny;
      d = 1 / d;
      const del = d * c;
      h *= del;
      if (Math.abs(del - 1) < 3e-14) break;
    }
    return Math.exp(-x + a * Math.log(x) - gln) * h;
  },
  pF: (F, d1, d2) => Stats.ibeta(d2 / (d2 + d1 * F), d2 / 2, d1 / 2),
  pT: (t, df) => Stats.ibeta(df / (df + t * t), df / 2, 0.5), // two-sided
  pChi2: (x, df) => Stats.gammaQ(df / 2, x / 2),
  pZ: z => Stats.gammaQ(0.5, z * z / 2), // two-sided

  // Average ranks over all values, plus the tie term sum(t^3 - t).
  ranks(values) {
    const order = values.map((v, i) => [v, i]).sort((a, b) => a[0] - b[0]);
    const ranks = new Array(values.length);
    let ties = 0;
    for (let i = 0; i < order.length;) {
      let j = i;
      while (j + 1 < order.length && order[j + 1][0] === order[i][0]) j++;
      const t = j - i + 1;
      ties += t * t * t - t;
      for (let k = i; k <= j; k++) ranks[order[k][1]] = (i + j) / 2 + 1;
      i = j + 1;
    }
    return { ranks, ties };
  },

  anova(groups) {
    groups = groups.filter(g => g.length > 0);
    const all = [].concat(...groups), N = all.length, k = groups.length;
    if (k < 2 || N <= k) return { p: NaN };
    const m = Stats.mean(all);
    const ssb = Stats.sum(groups.map(g => g.length * Math.pow(Stats.mean(g) - m, 2)));
    const ssw = Stats.sum(groups.map(g => { const gm = Stats.mean(g); return Stats.sum(g.map(x => (x - gm) * (x - gm))); }));
    if (ssw === 0) return { p: NaN };
    const df1 = k - 1, df2 = N - k, F = (ssb / df1) / (ssw / df2);
    return { F, df1, df2, p: Stats.pF(F, df1, df2), eta2: ssb / (ssb + ssw) };
  },

  kruskal(groups) {
    groups = groups.filter(g => g.length > 0);
    const all = [].concat(...groups), N = all.length, k = groups.length;
    if (k < 2 || N <= k) return { p: NaN };
    const { ranks, ties } = Stats.ranks(all);
    const correction = 1 - ties / (N * N * N - N);
    if (correction <= 0) return { p: NaN };
    let pos = 0, sum = 0;
    groups.forEach(g => {
      const r = Stats.sum(ranks.slice(pos, pos + g.length));
      sum += r * r / g.length;
      pos += g.length;
    });
    const H = (12 / (N * (N + 1)) * sum - 3 * (N + 1)) / correction;
    return { H, df: k - 1, p: Stats.pChi2(H, k - 1) };
  },

  welch(a, b) {
    if (a.length < 2 || b.length < 2) return { p: NaN };
    const va = Stats.variance(a), vb = Stats.variance(b);
    const se2 = va / a.length + vb / b.length;
    if (se2 === 0) return { p: NaN };
    const t = (Stats.mean(a) - Stats.mean(b)) / Math.sqrt(se2);
    const df = se2 * se2 / (Math.pow(va / a.length, 2) / (a.length - 1) + Math.pow(vb / b.length, 2) / (b.length - 1));
    const pooled = Math.sqrt(((a.length - 1) * va + (b.length - 1) * vb) / (a.length + b.length - 2));
    return { t, df, p: Stats.pT(t, df), d: (Stats.mean(a) - Stats.mean(b)) / pooled };
  },

  // Normal approximation with tie and continuity correction (as scipy's asymptotic method).
  mannWhitney(a, b) {
    const n1 = a.length, n2 = b.length, N = n1 + n2;
    if (!n1 || !n2) return { p: NaN };
    const { ranks, ties } = Stats.ranks(a.concat(b));
    const U = Stats.sum(ranks.slice(0, n1)) - n1 * (n1 + 1) / 2;
    const mu = n1 * n2 / 2;
    const s = Math.sqrt(n1 * n2 / 12 * ((N + 1) - ties / (N * (N - 1))));
    if (!(s > 0)) return { p: NaN };
    const z = Math.max(0, Math.abs(U - mu) - 0.5) / s;
    return { U, z: U < mu ? -z : z, p: Math.min(1, Stats.pZ(z)) };
  },

  // Contingency table: one row per group, [yes, no]. Pearson chi-square without continuity correction.
  chi2(table) {
    table = table.filter(r => r[0] + r[1] > 0);
    const N = Stats.sum(table.map(r => r[0] + r[1]));
    const cols = [Stats.sum(table.map(r => r[0])), Stats.sum(table.map(r => r[1]))];
    if (table.length < 2 || cols[0] === 0 || cols[1] === 0) return { p: NaN };
    let chi2 = 0, lowExpected = false;
    table.forEach(r => {
      const n = r[0] + r[1];
      [0, 1].forEach(j => {
        const e = n * cols[j] / N;
        if (e < 5) lowExpected = true;
        chi2 += (r[j] - e) * (r[j] - e) / e;
      });
    });
    const df = table.length - 1;
    return { chi2, df, N, p: Stats.pChi2(chi2, df), V: Math.sqrt(chi2 / N), lowExpected };
  },

  // diff = b - a (positive: higher in b).
  pairedT(a, b) {
    const d = b.map((x, i) => x - a[i]);
    if (d.length < 2) return { p: NaN };
    const sd = Stats.sd(d);
    if (!(sd > 0)) return { p: NaN };
    const diff = Stats.mean(d), t = diff / (sd / Math.sqrt(d.length));
    return { t, df: d.length - 1, p: Stats.pT(t, d.length - 1), dz: diff / sd, diff };
  },

  pearson(a, b) {
    const n = a.length;
    if (n < 3) return { p: NaN };
    const ma = Stats.mean(a), mb = Stats.mean(b);
    let sab = 0, saa = 0, sbb = 0;
    for (let i = 0; i < n; i++) {
      sab += (a[i] - ma) * (b[i] - mb);
      saa += (a[i] - ma) * (a[i] - ma);
      sbb += (b[i] - mb) * (b[i] - mb);
    }
    if (!(saa > 0 && sbb > 0)) return { p: NaN };
    const r = sab / Math.sqrt(saa * sbb);
    const t = r * Math.sqrt((n - 2) / Math.max(1e-15, 1 - r * r));
    return { r, p: Stats.pT(t, n - 2) };
  },
};

# Ultimatum Mirror Game: Project Handover

Research mini-game for a bachelor thesis. Players come from a Qualtrics survey, play a ~5 minute first-person sequence in the browser, and are redirected back to Qualtrics with their results. Built in Unity 6, WebGL, PC and mobile browsers. No download.

Developer works with Unity CLI and Claude Code. Keep scripts small, separated, and readable. No third-party packages unless explicitly approved (asset store characters are the exception, see Avatars).

---

## 1. Overall Flow

```
Qualtrics Survey A  ->  Game (WebGL)  ->  Qualtrics Survey B
(consent, gender,       (mirror tutorial,  (follow-up questions,
 species randomizer)     ultimatum game)    receives results via URL)
                              |
                              +-> Google Sheet (backup POST)
```

Data is linked across all three via `pid` (Qualtrics ResponseID from Survey A).

---

## 2. URL Parameters

### Incoming (Survey A -> Game)

| Param | Values | Meaning |
|---|---|---|
| `pid` | string | Qualtrics ResponseID |
| `g` | `m`, `w`, anything else | Gender from survey. `m`/`w` used directly, anything else or missing -> random 50/50 |
| `s` | `1`-`4` | Species, assigned and balanced by Qualtrics. Missing/invalid -> random |
| `debug` | `1` | Skips tutorial, shows debug overlay, does NOT post to Google Sheet |
| `a` | avatar species id | Test runs only (no `pid` or `debug=1`): shows this avatar regardless of `s`, e.g. `?a=slime&g=w`. Ignored in real runs |

Parse from `Application.absoluteURL`. If `pid` is missing, generate `test-<timestamp>` and treat as a test run (still playable, flagged in data).

### Outgoing (Game -> Survey B and Google Sheet)

| Field | Meaning |
|---|---|
| `pid` | from input |
| `g` | raw gender value received |
| `ag` | avatar gender actually used (`m`/`w`) |
| `s` | species actually used (1-4) |
| `r1` | Round 1: amount player keeps (0-100) |
| `r2` | Round 2: 1 = accepted, 0 = rejected |
| `r3` | Round 3: amount player keeps (0-100) |
| `r4` | Round 4: 1 = accepted, 0 = rejected |
| `t1`-`t4` | decision time per round in ms (from options shown to confirm) |
| `bal` | final balance in CHF |
| `dev` | `mobile` / `pc` (`Application.isMobilePlatform`) |
| `dur` | total duration in seconds (game start to submit) |
| `test` | `1` if no pid was provided or debug mode, else `0` |
| `ver` | build version string |

---

## 3. Game Sequence

### Scene 1: Mirror Room (Room 1)
- First person. Plain, minimal room.
- Mirrors on three walls (left, top, bottom in the design sketch). Body-sized, not wall-wide: just large enough for the player to see themselves fully, including jumps. Door on the fourth wall (right), closed.
- Player sees their own avatar in all three mirrors.
- Tutorial: a sequence of instructions shown as text. Player completes each with UI buttons (click adventure style, identical on PC and mobile).
  - Buttons: **arrow left** at the left screen edge and **arrow right** at the right edge (turn), **jump** at the bottom.
  - Example steps: "Dreh dich nach links", "Spring einmal", "Dreh dich einmal komplett um". Steps are defined in config (text + required action + count). Actions: `turnLeft`, `turnRight`, `jump`, `turnAround` (a full 360 deg turn in either direction, independent of `turnAngle`; count = number of full turns). Turns in the wrong direction are performed but not counted.
- After the last step: the door stays closed and becomes a tappable button **"Zur Tür"** (world-space UI on the door). The turn arrows stay visible so the player can turn towards the door.

### Transition
- Tapping the door: the player walks up to the door, the door opens, then automatic movement continues (scripted camera path or simple lerp) through the door into Room 2, ending in front of the NPC.
- Button **"Ansprechen"** appears. Pressing it starts the dialog.

### Scene 2: NPC Room (Room 2)
- One NPC: neutral human, as unrecognizable as possible (e.g. hooded figure, no clear facial features, neutral colors). Same NPC for all players regardless of avatar.
- NPC behaviour and appearance must not influence the player: idle animation only, no emotional reactions.

### Dialog: Ultimatum Game (4 rounds)
- Dialog cannot be exited. No close button, no movement, no back navigation.
- Balance displayed permanently. Starts at 0 CHF.
- Each round, 100 CHF of new, neutral money comes into play. The player's share is added to the balance if the split is accepted.
- After each round, show the result (accepted/rejected, what each received), then continue.

| Round | Who splits | Rule |
|---|---|---|
| 1 | Player | Player chooses how much to keep, 0-100 in steps of 5. NPC accepts if player keeps <= 90, otherwise rejects (both get 0) |
| 2 | NPC | NPC offers 50:50. Player accepts or rejects |
| 3 | Player | Same as round 1 |
| 4 | NPC | NPC keeps 75, offers player 25. Player accepts or rejects |

Split input for rounds 1 and 3: slider or +/- buttons, must be comfortable on touch screens. Show both shares live ("Du: 60 CHF / Gegenüber: 40 CHF"). Explicit confirm button.

### End
- Short end text ("Danke, du wirst jetzt zur Umfrage weitergeleitet").
- POST result to Google Sheet (max 5 s wait, continue on timeout or error).
- Redirect to Survey B with all result fields as query params.
- Guard against double submission.

Target total duration: about 5 minutes.

---

## 4. Avatars

- 4 species x 2 genders. The thesis author has not fixed the 4 species yet, so 8 candidates are built and `config.json` (`species`) maps `s` = 1-4 to candidate ids:

  | id | Model | Variants |
  |---|---|---|
  | `humanC` | Synty `SM_Gen_Chr_Street_Male_02` / `_Female_02` (PDF letter C) | m/w |
  | `humanI` | Synty Office `SM_Chr_Business_Male_02` / `_Female_02` (I) | m/w |
  | `goblin` | Synty Dungeon `SM_Chr_Goblin_Male_01` / `_Female_01` (L) | m/w |
  | `robot` | Synty `SM_Gen_Chr_Robot_01` (R) | one, used for m and w |
  | `skeleton` | Synty `SM_Gen_Chr_Skeleton_01` | one |
  | `slime` | Creatures_3D slime, scaled 1.4 | one |
  | `mushroom` | Lil Pupinduy `Mushroom_1` | one |
  | `cow` | Backrock Low Poly Animals `Cow_Charolais` | one |

  Default mapping: 1 `humanC`, 2 `goblin`, 3 `robot`, 4 `cow`.
- Size (decided 2026-10-30): humanoids keep their size, creatures are roughly human-sized, the camera sits at each avatar's own eyes (humans 1.67 m, cow 1.17 m, mushroom 0.9 m, slime 0.66 m).
- NPC: "V" from the Modular Fantasy Hero kit, grey hood + face cloth + long robe (`Assets/Prefabs/NPC.prefab`, material `Art/NPC/NPC_Grey.mat`).
- No exaggerated gender features. Neutral colors. Differences only through e.g. long/short hair, angular/round face shape, broad/narrow shoulders.
- Player cannot choose an avatar. Selected from `ag` + `s`.
- Characters will come from the Unity Asset Store (free if possible). Build the avatar system so models can be swapped easily: an `AvatarDatabase` ScriptableObject mapping (species, gender) -> prefab.
- All avatar prefabs share `Animations/Avatar.controller` (Idle, Jump with trigger `Jump` and speed parameter `JumpSpeed`). Humanoids and the NPC use the generated clips `Humanoid_Idle` / `Humanoid_Jump`; slime, mushroom and cow use `AnimatorOverrideController`s with their own clips (cow: Run as jump). No turn animation.
- Avatar prefab layout: root (`AvatarBody`) on the ground directly below the eyes, child `Eyes` marker, the model as nested prefab offset so its eyes sit above the root. `AvatarBody` lists the bones collapsed in first person; separate meshes (mushroom hat, eyebrows) are on layer `FirstPersonHidden` instead.
- Editor tools (menu **Mirror Game**): `Build Humanoid Clips` (`Editor/HumanoidClipBuilder.cs`, poses as muscle values, jump timing matches `PlayerActions`), `Build Avatars` (`Editor/AvatarBuilder.cs`: controller, overrides, all prefabs, NPC, `Data/AvatarDatabase.asset`). Rerun after changing a model, scale or eye position.
- Licensing: Synty, Creatures_3D, Lil Pupinduy and Backrock folders are git-ignored (public repo). Prefabs/controllers in the repo only reference them, so a fresh clone cannot build without the packs.

---

## 5. Mirrors (Fake Mirror Technique)

No reflection cameras or render textures. Mirrors are faked with copied geometry:

- Behind each mirror wall, a reflected copy of Room 1 is placed. Reflection via a parent transform at the mirror plane with negative scale on the axis perpendicular to the mirror (Unity handles winding order for negatively scaled objects).
- Each mirror world contains a clone of the player avatar. Every `LateUpdate`, the clone copies the player avatar's local pose relative to the room (position, rotation, and either Animator parameters/state or bone transforms). Bone copying is preferred if Animator sync looks off.
- The mirror surface itself is an opening in the wall, optionally with a very subtle transparent tinted glass quad and a frame for readability.
- Only first-order reflections are needed. Opposite mirrors (top/bottom walls) do not need infinite reflections: inside each copied room, the other mirror walls are rendered as plain darkened mirror surfaces.
- Mirror worlds must not be visible from anywhere except through their mirror. Make sure the copy behind the left mirror does not overlap Room 2.
- First person: the real player avatar's head is hidden from the main camera. Mirror clones render the full avatar including head. Synty models are one skinned mesh, so `FirstPersonHider` scales the head bone to zero in `LateUpdate` (after `MirrorAvatarSync` copied the real scale) and restores it in `Update` (before the Animator). Separate renderers can use layer `FirstPersonHidden` (6).
- Turning and jumping must be clearly visible in the mirrors, that is the point of the tutorial.

**Implementation (milestone 3):**
- `MirrorWorld` (one per mirror, under `World/MirrorWorlds`) sits on the mirror plane (inner wall face) with local z into the wall and scale (1, 1, -1). At startup it instantiates a copy of `World/Room1` under itself, removes its own `MirrorWall` from the copy and switches the other mirrors in the copy to an opaque dark material (`MirrorWall.ShowAsDark`). Moving parts of Room 1 (door hinge) are synced every `LateUpdate`.
- Everything that should appear in the mirrors must be a child of `Room1`. Room 1 objects must not be static-batched (instantiated copies of batched renderers keep drawing at the original position).
- `MirrorWorld.SetAvatar` clones the player avatar, moves layer `FirstPersonHidden` parts to Default, disables Animators, removes `FirstPersonHider` and adds `MirrorAvatarSync`, which reflects the root pose and copies all bone local transforms. `AvatarSpawner` calls `SetAvatar` after spawning.
- First person: see `FirstPersonHider` above (bones) and layer `FirstPersonHidden` (6, not rendered by the main camera) for separate meshes.
- Mirror walls are thin (0.02 m) so the opening has no depth that would hide part of the reflection.
- Lighting must look the same in the reflection: the directional light points straight down (unchanged by reflection at vertical planes). Baked lighting later must also be symmetric or be baked into the copies.
- Through the reflected door, Room 2 is not shown (dark). Only visible if the player looks into a mirror while the door is open, which the flow does not allow.

---

## 6. Controls and UI

- Only UI buttons that are tapped/clicked, identical on PC and mobile. No keyboard input, no mouse look, no joysticks. Interactions with objects in the world (door) are tappable world-space UI buttons on the object.
- Turn left/right: smooth rotation by a fixed angle (configurable, default 90 deg).
- Jump: simple jump with animation.
- Buttons large enough for touch (min ~80 px at 1080p reference, Canvas Scaler "Scale With Screen Size").
- Orientation: design for landscape. In portrait on mobile, show an overlay "Bitte Gerät drehen" until rotated.
- Language: Standard German (Hochdeutsch). All texts come from config, final wording will be written by the thesis author.

---

## 7. Config

`Assets/StreamingAssets/config.json`, loaded at startup via UnityWebRequest (WebGL cannot read StreamingAssets with File IO). Allows text and parameter changes without code changes.

```json
{
  "version": "0.1.0",
  "moneyPerRound": 100,
  "splitStep": 5,
  "npcAcceptMaxPlayerKeeps": 90,
  "round2NpcKeeps": 50,
  "round4NpcKeeps": 75,
  "turnAngle": 90,
  "species": ["humanC", "goblin", "robot", "cow"],
  "submitTimeoutSeconds": 5,
  "appsScriptUrl": "https://script.google.com/macros/s/XXXX/exec",
  "returnUrlTemplate": "https://XXXX.qualtrics.com/jfe/form/SV_XXXX?pid={pid}&g={g}&ag={ag}&s={s}&r1={r1}&r2={r2}&r3={r3}&r4={r4}&t1={t1}&t2={t2}&t3={t3}&t4={t4}&bal={bal}&dev={dev}&dur={dur}&test={test}&ver={ver}",
  "tutorialSteps": [
    { "text": "Dreh dich nach links.", "action": "turnLeft", "count": 2 },
    { "text": "Dreh dich nach rechts.", "action": "turnRight", "count": 2 },
    { "text": "Spring einmal.", "action": "jump", "count": 1 },
    { "text": "Dreh dich einmal komplett um.", "action": "turnAround", "count": 1 }
  ],
  "texts": {
    "intro": "PLACEHOLDER",
    "goToDoorHint": "PLACEHOLDER",
    "jump": "Springen",
    "goToDoor": "Zur Tür",
    "talk": "Ansprechen",
    "npcGreeting": "PLACEHOLDER",
    "round1Intro": "PLACEHOLDER",
    "round2Intro": "PLACEHOLDER",
    "round3Intro": "PLACEHOLDER",
    "round4Intro": "PLACEHOLDER",
    "accept": "Annehmen",
    "reject": "Ablehnen",
    "confirm": "Bestätigen",
    "npcAccepted": "PLACEHOLDER",
    "npcRejected": "PLACEHOLDER",
    "playerAccepted": "Du hast das Angebot angenommen.",
    "playerRejected": "Du hast das Angebot abgelehnt.",
    "splitShares": "Du: {player} CHF / Gegenüber: {npc} CHF",
    "npcOffer": "Dein Gegenüber behält {npc} CHF und bietet dir {player} CHF an.",
    "roundResult": "Du erhältst {player} CHF, dein Gegenüber erhält {npc} CHF.",
    "next": "Weiter",
    "balance": "Kontostand",
    "end": "Danke! Du wirst jetzt zur Umfrage weitergeleitet.",
    "rotateDevice": "Bitte Gerät drehen"
  }
}
```

Fallback: if config fails to load, use built-in defaults so the game never breaks.

---

## 8. Architecture

```
Assets/
  Scripts/
    Core/        GameFlow.cs, GameState.cs, Config.cs, ConfigLoader.cs, UrlParams.cs
    Tutorial/    TutorialController.cs, TutorialProgress.cs, PlayerActions.cs
    World/       Door.cs, WalkPath.cs
    Mirror/      MirrorWorld.cs, MirrorWall.cs, MirrorAvatarSync.cs
    Avatar/      AvatarDatabase.cs (ScriptableObject), AvatarSpawner.cs, AvatarBody.cs, FirstPersonHider.cs
    Dialog/      UltimatumController.cs, SplitInputUI.cs, BalanceUI.cs
    Data/        SessionData.cs, IResultSubmitter.cs, ResultSubmitter.cs, ResultJson.cs, Redirector.cs
    UI/          UIManager.cs, ActionButtonsUI.cs, WorldButton.cs, OrientationOverlay.cs, DebugOverlay.cs
  Plugins/WebGL/ Redirect.jslib
  Editor/        BuildScript.cs, HumanoidClipBuilder.cs, AvatarBuilder.cs
  Animations/    Avatar.controller, Humanoid_Idle/Jump.anim, *.overrideController
  Prefabs/       Avatars/Avatar_<id>[_m|_w].prefab, NPC.prefab
  Data/          AvatarDatabase.asset
  StreamingAssets/config.json
  Scenes/        Main.unity
```

One scene. Room 1, Room 2, mirror worlds and NPC all live in `Main.unity`.

**Scene layout** (greybox, milestone 2): both rooms 6 x 6 m interior, 3 m high, walls 0.2 m. Room 1 centred at the origin, mirrors on -x (left), +z (top), -z (bottom), door (1.2 x 2.2 m) in the +x wall. Room 2 directly behind it, centre x = 6.2. Player rig (`PlayerRig`, with `PlayerActions`, camera at 1.6 m and the avatar as children) starts at the origin facing +z. `WalkToDoor` stops at (2.2, 0, 0) facing the door, then the door opens; `WalkToNpc`: doorway (3.1, 0, 0), then (6.0, 0, 0) facing +x; NPC at (7.4, 0, 0) facing -x. Mirrors are body-sized (1.5 x 2.4 m, bottom edge 0.15 m above the floor) centred on each mirror wall (`World/Room1/MirrorWall*`), with a dark frame and a subtle glass tint. The door hinge is part of Room 1. The avatar is spawned into `PlayerRig` at startup (`Game/AvatarSpawner`), which also moves the camera to the avatar's eye height. The NPC prefab sits under `NPC`. Jump: 0.15 s windup, 0.6 s in the air, 0.2 s landing (`PlayerActions`); the avatar's jump clip is stretched to that duration.

**GameFlow states:** `Init -> Tutorial -> GoToDoor -> WalkToNPC -> Dialog -> Submit -> Redirect`

`SessionData` holds all recorded values and the start timestamp. Only `ResultSubmitter` and `Redirector` read it at the end.

Reloading the page restarts the game. Data is only submitted at the end.

---

## 9. Backend: Google Sheet

Apps Script, deployed as Web App with access "Anyone":

```javascript
function doPost(e) {
  const sheet = SpreadsheetApp.getActiveSpreadsheet().getSheets()[0];
  const d = JSON.parse(e.postData.contents);
  const cols = ["pid","g","ag","s","r1","r2","r3","r4","t1","t2","t3","t4","bal","dev","dur","test","ver"];
  if (sheet.getLastRow() === 0) sheet.appendRow(["timestamp", ...cols]);
  sheet.appendRow([new Date(), ...cols.map(c => d[c] ?? "")]);
  return ContentService.createTextOutput("ok");
}
```

Important: send the POST with `Content-Type: text/plain` (JSON in the body) to avoid a CORS preflight, which Apps Script does not answer. Use `UnityWebRequest` with an `UploadHandlerRaw` and set the header manually.

Note: the university may have data storage rules. Keep the submitter behind an interface so the backend can be swapped later.

---

## 10. Redirect

`Application.OpenURL` opens a new tab in WebGL and gets blocked as popup. Use a jslib:

```javascript
// Assets/Plugins/WebGL/Redirect.jslib
mergeInto(LibraryManager.library, {
  RedirectTo: function (urlPtr) {
    // replace(): game is not kept in history, "back" in Survey B cannot start a second run
    window.location.replace(UTF8ToString(urlPtr));
  }
});
```

```csharp
[DllImport("__Internal")] private static extern void RedirectTo(string url);
```

In the Editor, log the URL instead of redirecting. URL-encode all values.

---

## 11. Qualtrics Setup (reference for the thesis author)

**Survey A**
1. Questions: consent, gender, other pre-questions.
2. Survey Flow: after the gender question, Branch logic sets Embedded Data `gender` to `m` or `w`. Other answers set `gender = x` (game randomizes).
3. Survey Flow: Randomizer with 4 Embedded Data elements (`species = 1`, `2`, `3`, `4`), "Randomly present 1 of the following elements", **Evenly Present Elements** enabled.
4. End of Survey: Redirect to URL
   `https://<host>/?pid=${e://Field/ResponseID}&g=${e://Field/gender}&s=${e://Field/species}`
   Free Qualtrics accounts cannot redirect ("Für Ihre Instanz nicht verfügbar"). Workaround used in the test setup: End of Survey -> Customize -> "Nachricht zum Umfragenende aus der Bibliothek" with a link (`<a href="<URL above>" target="_self">`). Piped fields work in that message. Switch back to a real redirect on a licensed account.

**Survey B**
1. First element in Survey Flow: Embedded Data block with fields `pid, g, ag, s, r1, r2, r3, r4, t1, t2, t3, t4, bal, dev, dur, test, ver` (values left empty, Qualtrics fills them from the URL).
2. Follow-up questions.
3. Datasets are merged via `pid`.

Note: "Evenly Present" counts assignments, not completions. Dropouts can unbalance groups slightly.

---

## 12. Build and Hosting

- Unity 6, URP, WebGL.
- `BuildScript.BuildWebGL()` callable via:
  ```
  Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWebGL -logFile build.log
  ```
  Output to `Builds/WebGL`.
- Compression: Gzip with **Decompression Fallback** enabled (GitHub Pages does not set Content-Encoding headers).
- Hosting: GitHub Pages (or Netlify / Cloudflare Pages). Not itch.io, its iframe does not pass URL parameters.
- Deployed at https://kingminato.github.io/Nina-Bachelor/ from the `gh-pages` branch (root). That branch only holds the contents of `Builds/WebGL` plus an empty `.nojekyll`; `Builds/` stays ignored on `main`. To deploy: build, then replace the files on `gh-pages` (e.g. via `git worktree add`), commit, push.
- Performance targets for mobile browsers: 30 fps, baked lighting, no realtime shadows, low poly, texture size max 1024, keep build small (< 30 MB). Room is minimal so this should be easy, but mirror worlds multiply geometry and avatars by 4.
- Mobile-friendly WebGL template: full window canvas, loading bar, no Unity footer.

---

## 13. Milestones

Status: milestone 1 done (2026-10-27). Full chain Survey A -> Game -> Sheet -> Survey B verified 2026-10-29. Milestone 2 done (2026-10-29). Milestone 3 done (2026-10-29). Milestone 4 built 2026-10-30 (all 8 candidates and the NPC checked in the mirrors in play mode), waiting for review in Unity and the thesis author's final 4 species. Next: milestone 5.

1. **Foundation** (done): BuildScript, config loading, URL params, GameFlow with placeholder UI, ultimatum logic, SessionData, submit + redirect. Primitives only. Goal: full run Qualtrics -> Game -> Sheet -> Qualtrics works end to end.
2. **Rooms and movement** (done): Room 1 and 2 greybox, door, turn/jump buttons, tutorial steps from config, walk to NPC.
3. **Mirrors** (done): mirror worlds, avatar clones, first-person head hiding.
4. **Avatars and NPC** (built): asset store characters, AvatarDatabase, 8 candidate species, neutral NPC.
5. **Polish**: final texts from thesis author, UI styling, orientation overlay, mobile testing, performance pass.
6. **Pilot**: test with a few people, check timing (~5 min) and data in Sheet and Qualtrics.

---

## 14. Test Checklist

- [ ] Game starts without params (test mode, random avatar)
- [ ] `g=m`, `g=w`, `g=x`, `g` missing all handled correctly, `ag` logged
- [ ] `s=1`-`4` and invalid `s` handled
- [ ] `debug=1` skips tutorial, no Sheet POST
- [ ] Round 1/3: keeps 90 -> accepted, keeps 95 -> rejected, keeps 100 -> rejected
- [ ] Round 2/4 accept and reject both recorded correctly
- [ ] Balance correct in all combinations
- [ ] Dialog cannot be left
- [ ] Decision times plausible
- [ ] Sheet row arrives, correct columns
- [ ] Redirect works when Sheet is unreachable (timeout path)
- [ ] All values arrive in Qualtrics Survey B
- [ ] No double submission when buttons are tapped repeatedly
- [ ] Chrome, Firefox, Safari desktop
- [ ] Safari iOS, Chrome Android, landscape and portrait overlay
- [ ] Total duration about 5 minutes

---

## 15. Open Points

- Final 4 species not decided yet (8 candidates built, mapping in `config.json`).
- Final texts (intro, tutorial, NPC dialog) will be written by the thesis author.
- Data storage rules of the university unknown. Google Sheet is the working assumption.
- Texture sizes of the character packs not yet limited to 1024 (performance pass, milestone 5).
- Test setup runs on the developer's Google account (Sheet + Apps Script) and free Qualtrics account (Survey A "Mirror Game – Teil A": https://qualtricsxmbxwbdbh7v.qualtrics.com/jfe/form/SV_5iPvenZKKZp0isC, Survey B "Mirror Game – Teil B"). Before the pilot, move everything (Sheet, Apps Script, Qualtrics surveys, e.g. via .qsf export/import) to the thesis author's account and update `appsScriptUrl` and `returnUrlTemplate` in config.json.
- Thesis author should check whether the university has a Qualtrics license with URL redirect (otherwise keep the link workaround from section 11).
- Before the pilot, delete all test rows/responses in the Sheet, Survey A and Survey B.
- Decisions to confirm with the thesis author: split slider starts at 50:50 (possible anchoring); round intro text is shown together with the options, so decision time includes reading time; decision time starts when options appear, including the 0.4 s input lock. Tutorial: actions other than the requested one are performed but not counted; the NPC greeting is shown as the first dialog line after "Ansprechen".

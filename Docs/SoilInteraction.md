# Grunto ir ekskavatoriaus sąveika

Unity 6000.6.3f1, URP, XR Interaction Toolkit ir OpenXR. Level 1 turi kaušą, Level 2 – rąstų griebtuvą. Griebtuvas nepakeistas kaušu.

## Fizikos modelis ir pataisymai

1. **Važiuoklės atrama.** `ExcavatorSuspension` taiko dinaminiam šakniniam Rigidbody atskiras spyruoklės ir slopintuvo jėgas dvylikoje vikšrų taškų. Peilio taškas nebenaudojamas kaip papildomas visos mašinos aukščio matmuo. Peilio / kaušo reakcija perduodama per `AddForceAtPosition` tikroje kontakto vietoje. Jėgos riba apskaičiuojama pagal masių centro momentą apie priešingus vikšrų taškus. Atitinkamas galas gali pakilti; gravitacija ir slopintuvai grąžina mašiną pakėlus įrankį. Krovinys ir laikomas rąstas keičia masę bei masių centrą. Kaušo kinematinis Rigidbody paleidžiant pašalinamas: jo apvalkalo kontaktai priklauso bendram važiuoklės kūnui. Atskirai likęs kinematinis vaikinis kūnas laikė kaušą kitoje pasaulio vietoje ir savo kontaktais galėjo stumti pačią važiuoklę į reljefą.
2. **Minkštas griebtuvo kontaktas ir rąstai.** Mašinos reljefo kontaktus sprendžia atramos ir deformacija, todėl TerrainCollider nebeišstumia judančio įrankio staigiu impulsu. Griebtuvo žnyplėms leidžiama iki 6 cm valdoma minkšto grunto kontakto zona. Judinamos jos plonai nugramdo gruntą ir perkelia jį šalia, išsaugodamos tūrį. Kietos kliūtys lieka tikrinamos. Įrankių medžiaga turi nulinį atšokimą; judinamiems objektams taikoma ribota jėga, 0,35 m/s išstūmimo riba ir slopinimas. Ankstesnis tiesioginis rąsto Rigidbody pozicijos taisymas pašalintas. Žnyplių subžingsniai dalijasi vieno žingsnio jėgos biudžetu. Dinaminės atramos slėgio užklausa nebetikrina ir neatstato visos važiuoklės pozos kiekvieno įrankio kontakto metu; jau užblokuotai pradinei pozai nebevykdomos septynios beprasmės visos kontaktų geometrijos paieškos. Jungtinis žnyplės kontaktas ir jo vaikiniai kontaktai nebeapdoroja to paties paviršiaus pakartotinai.
3. **Gilus kasimas.** Pasipriešinimą daugiausia lemia medžiaga, atakos kampas ir greitis. Gylio priedas švelnus ir ribotas. Abiejose scenose molio sluoksnio pradžia perkelta į 1,5 m, sutankinto – į 3,5 m. Tikras pradinis strėlės diapazonas leido dantims pasiekti tik apie 1,26 m žemiau vikšrų: Bone_1 didžiausias kampas padidintas nuo 30° iki 60°, geometrinis pasiekiamumas dabar apie 2,28 m. Krovinio trigger dėžė nebelaikoma uždaru plieno tūriu: tikrinamas atviras kauso apvalkalas. Dantų atakos kampas vertinamas pagal tikro plieninio apvalkalo apačią, ne neegzistuojančius žemai pasisukusios krovinio dėžės kampus. Dantų pjovimo plotis atsižvelgia į Terrain langelio dydį; ties sąlyčiu priimama saugi dalis judesio, galima slysti negiliu kontaktu ir ištraukti įrankį. Į duobės kraštą atsirėmęs šoninis pjaunantis kraštas gali nustumti ploną sluoksnį į šoną, išsaugodamas gruntą vietiniame pylimėlyje; tai nepildo kaušo ir neleidžia kasti spaudžiant dugno vidurį žemyn.
4. **Duobės ir šlaitai.** Vikšrų taškai gali atskirai prarasti atramą. Važiavimo riba scenose – 35°. Stačias laiptas savaime netampa įveikiamu šlaitu: reikia nuvažiuojamos rampos arba ją suformuoti įrankiu. Slinkimas tvarko sutrikdytą gruntą ir pažadina gretimus blokus tik kai iš tikrųjų pasikeičia jų riba. Natūralūs nejudinti šlaitai nėra globaliai išlyginami.
5. **Peilis ir vikšrai.** Peilio judesys nukerpa vietinį sluoksnį, užpildo žemesnes vietas priekyje, kaupia pylimą ir leidžia daliai grunto nutekėti pro galus. Supilama tik tiek, kiek pašalinama. Pašalintas absoliutus pradinio paviršiaus gylio draudimas, blokavęs darbą iškastos duobės dugne. Stumiamo tūrio apkrova lėtina važiavimą. Minkšto grunto vikšrų vėžės lieka negilios ir išstumia gruntą į kraštus.
6. **Reljefas ir kolideris.** Viena aukščių talpykla aptarnauja dantis, įrankių kontaktus ir vaizdo pakeitimus. Po visų komandinių judesių kiekviename FixedUpdate vykdomas `SyncHeightmap`. Fizinė ir vaizdinė geometrija nebelaukia ankstesnio 80 ms tarpo. Paviršius imamas pagal tuos pačius trikampius kaip Unity Terrain. Slinkimas mažina naujų iškasų / pylimų briaunas, nekurdamas papildomo tūrio.
7. **Tolimos dėmės.** Dažomi tik iš tikrųjų pakeisti aukščių langeliai, todėl netaisyklingas natūralus reljefas pats savaime nepalieka rudų dėmių. Deformacija ir efektai atmeta netaisyklingas koordinates ir vietas už darbo spindulio. Aktyvuojant fondo grunto objektą iš naujo nustatoma ir Rigidbody poza, kad nepasireikštų sena interpoliuota pozicija.
8. **Jokio talpos UI.** `SoilWorkFeedback` nebekuria TMP rodmens; pašalinti jo scenų nustatymai. Vidinė tūrio ir masės apskaita bei palaipsniui mažėjantis kauburio tinklas išlaikyti. Dulkių ir garso efektai lieka.

Važiavimo X/Z ir kabinos posūkio komandos tebėra valdomos saugomais pozos žingsniais; Y, posvyris ir pasvirimas į šoną sprendžiami dinaminiu Rigidbody. Strėlė ir žnyplės naudoja esamus ribotus FK judesius, ne ArticulationBody vyrių grandinę. Gruntas – konservuojamas aukščių žemėlapis su ribotu krentančių gabalų fondu, ne pilna birių dalelių simuliacija.

## Inspector derinimas

| Komponentas | Reikšmės ir poveikis |
|---|---|
| `ExcavatorGrounding` | `physicalSuspension=true`; `suspensionCompression=0.018` m, `suspensionDamping=1`, `contactReach=0.06` m. Didesnis slopinimas mažina šokinėjimą. `maxDriveSlope=35`, `maxStepHeight=0.18` m. |
| `ExcavatorGrounding` | `maximumHydraulicForce=18000` N, `supportForceMargin=1.2`, `toolCompliance=0.025` m, `maxHydraulicTilt=10`°. Didinti jėgą saikingai: ilgesnis petys natūraliai lengviau pakelia mašiną. |
| `ExcavatorMass` | Bazė 1700 kg, strėlė 170, ranka 100, įrankis 70; `baseCenterOfMassMetres` pateiktas metrais, nepriklauso nuo FBX mastelio. |
| `DigTeeth` | `depthResistanceMultiplier=0.08`, `maxResistance=4`, `speedResistanceMultiplier=0.12`, `maxDepthPerStep=0.03` m. Bendro pasipriešinimo ribojimas nepanaikina kietų kliūčių patikros. |
| `SoilMaterialProfile` | `resistance`, `angleOfRepose`, `cohesion`, `friction`, `softness`, `density`; keturi profiliai `Assets/Soil`. Loose dirt slinkimo kampas 34°. |
| `DigSystem` | `clayDepth=1.5`, `compactedDepth=3.5` m, `maximumWorkDistance=8` m. Spindulys turi apimti visą įrankio pasiekiamumą ir išpylimo vietą. |
| `GroundedAttachment` | Griebtuvo zoną prieš Play keičia `ExcavatorContacts.softGripperDepth=0.06`; kuriamiems kontaktams `canScrapeSoil=true`; peiliui `canPushSoil=true`. Minkštos zonos nedidinti tiek, kad įrankis laisvai prasmuktų pro gruntą. |
| `ExcavatorBody` | `dynamicContactSkin=0.018` m, `maxObjectPushSpeed=0.35` m/s, `maxObjectPushForce=1200` N. |
| `DigSystem`, `SoilWorkFeedback` | 32 laisvi dinaminiai grunto gabalai, 2 šlaitų blokai per ciklą, 2 dažymo blokai, 128 dulkių dalelės. |

## Play režimo patikra

1. Abiejuose lygiuose lėtai nuleisti peilį, tada spausti toliau. Stebėti iš šono: turi kilti peilio galas, priešingas vikšrų galas likti atramoje. Pakelti peilį ir pakartoti su kaušu. Keisti strėlės petį ir krovinį; nusėdimas neturi šokinėti.
2. Level 2 lėtai prakišti atidarytą griebtuvą po rąstu prie žemės, užspausti, pakelti, pasukti ir paleisti. Žnyplės gali negiliai gramdyti, rąstas neturi būti išmetamas kontakto impulsu. Patikrinti ir kietą kliūtį: ji neturi tapti pereinama.
3. Level 1 kasti sluoksniais, reguliariai išpilti krovinį į atskirą vietą ir plėsti duobės kraštus. Naudoti nuleidimą, rankos pritraukimą ir kaušo užlenkimą kartu. Palyginti žemės paviršių su 2 m gylio žymekliu. Vien vertikaliai spaudžiant uždarą dugną žemės neturi patekti į kaušą.
4. Suformuoti negilią duobę ir 30–35° išvažiavimo rampą. Važiuoti abiem kryptimis, sustoti ant rampos; stebėti vikšrų kontaktus. 90° siena nėra važiavimo rampa.
5. Peiliu stumti mažą pylimą į negilią duobę: už peilio turi žemėti, priekyje ir pro galus – kauptis, duobė pildytis. Su didesniu pylimu greitis turi mažėti. Palyginti vėžes minkštame ir sutankintame grunte.
6. Kasti ir pilti, sustoti, padėti rąstą ant pakeisto paviršiaus. Įjungus Physics Debugger kolideris turi sutapti su reljefu. Stebėti, ar toli nuo darbo vietos nekinta nei aukštis, nei ruda tekstūra.
7. Kabinoje neturi būti jokio talpos teksto; užpildymas matomas tik kauše. Greitai išversti: užpildas mažėja kartu su srautu, žemė lieka nusileidimo vietoje.
8. Quest 2 build'e matuoti CPU Physics, Terrain, skriptus, GC ir GPU kasant, sukant bei ilgai pilant. 72–90 FPS aparatine įranga dar nepatvirtintas. Jei reikia, pirmiausia mažinti šlaitų / dažymo blokų ir efektų biudžetus, ne grąžinti koliderio delsą.

## Automatinių bandymų vykdymas

Bandymai skirti atskirai projekto kopijai, nes užbaigia Unity procesą. Nenaudoti ant atidaryto pagrindinio projekto.

- `RealisticControlValidation.Run`: ankstesnio valdymo, krovinio, kasimo, peilio, garso, masių ir scenų regresijos.
- `FollowupPhysicsValidation.Run`: natūralios taškinės jėgos, atrama masteliais 1 ir 0,02, tikras strėlės pasiekiamumas, išvažiavimas 30° rampa, natyvaus reljefo koliderio sutapimas, darbo zonos ir dėmių ribos, minkštas kontaktas su rąstu ir tikras scenos peilis.
- `FollowupPhysicsValidation.RunScenes`: abiejų tikrų scenų dinaminės atramos nusėdimas, visų kaušo kontaktų priklausymas bendram važiuoklės Rigidbody, kaušo vietos ant strėlės išlaikymas judant važiuoklei ir užblokuoto Level 2 griebtuvo kontakto laiko / paskirstymų matavimas.
- `FollowupPhysicsValidation.RunDeep`: kartojami tikros Level 1 geometrijos ribotų sąnarių pjovimo ciklai, krovinio iškrovimo ir nusėdimo apskaita. Šiame izoliuotame pjovimo bandyme šlaitų slinkimas išjungtas; slinkimas ir atramos tikrinami atskiruose bandymuose. Tai nepakeičia rankinio VR bandymo.

Scenų pakeitimai skirti Level 1 ir Level 2. Naudotojo meniu ir šablonų pakeitimai į fizikos pataisą neįtraukiami.

Po skriptų atnaujinimo sustabdyti Play ir iš naujo paleisti sceną: nauja kontaktų hierarchija paruošiama Awake, o jau vykstančio Play sugadinta įrankio poza nėra laikoma teisinga pradine scena.

Fizikos regresijų rezultatai atskiroje Unity kopijoje: abi tikros scenos nusėda virš reljefo (mažiausias vikšrų tarpas apie 3–5 mm), kaušas nekeičia savo vietos ant strėlės judant dinaminei važiuoklei. Level 2 grunto kontakto bandyme aktyvus gramdymas su reljefo sinchronizavimu vidutiniškai truko apie 3,4 ms vienam žingsniui ir po apšilimo nekūrė nuolatinių GC paskirstymų. Tai kompiuterio Editor batch matavimas; VR įrenginio FPS jis negarantuoja.

Galutinė kasimo regresija pasiekė 2,032 m gylį per tikrus ribotų Level 1 sąnarių pjovimo ciklus; iškrauta apie 0,535 m³ surinkto grunto. Iškrovimas šiame izoliuotame bandyme atliekamas testiniu krovinių iškėlimo / nusėdimo pagalbininku, o tikras palaipsnis kaušo išpylimas atskirai tikrinamas bendroje regresijų grupėje. Šlaitų slinkimas gylio bandyme išjungtas. `RealisticControlValidation.Run`, `FollowupPhysicsValidation.Run`, `FollowupPhysicsValidation.RunScenes` ir `FollowupPhysicsValidation.RunDeep` galutinėse patikrose praėjo.

## Vietinis supylimas ir atramos stabilizavimas

Šis papildomas pakeitimas peržiūrėtas pagal logiką; naudotojo prašymu Unity ir automatiniai bandymai nepaleisti. Ankstesni bandymų rezultatai aukščiau priklauso ankstesnei versijai.

- Kaušo šonai ir griebtuvas žemes perkelia greta kontakto, ne per kelis darbo spindulius. `DigSystem.looseSoilEdgeGap` numatyta 0,025 m. Peilio pylimas taip pat priartintas prie jo krašto.
- Pagal vikšrų geometriją ir 0,15 m paraštę saugomas važiuoklės plotas: ten neleidžiamas supylimas, įrankių kasimas ar šlaitų grunto perkėlimas. Vikšrų negilios vėžės apdorojamos atskirai. Blokuotas perkėlimas nekeičia pradinio grunto, todėl nesukuria pakaitinio kupsto po mašina.
- Atramos jėga glodinama per `supportForceSmoothing=0.15` s; kėlimo momentui leidžiamas `liftForceSurplus=0.06` priedas. Slopinimas remiasi kontaktinio taško efektyvia mase; galutinė jėga apribojama ir po slopinimo.
- Kritimo greitis prie atramos ribojamas pagal numatomą vikšro kontaktą. Jei vikšras vis dėlto persidengė su reljefu, leidžiama tik maža atkuriamoji korekcija, o ne laukimas, kol suspaustos spyruoklės išmes užkastą mašiną. `terrainContactSkin=0.005` m.

Sustabdyti Play ir paleisti sceną iš naujo, tada rankiniu būdu pabandyti peilio kėlimą, jo laikymą nuleistoje padėtyje ir žemių stūmimą arti vikšrų.

## Peilio kontaktas, tikras griebimas ir meniu

Papildymas peržiūrėtas pagal kodą, be Unity paleidimo ir automatinių bandymų, kaip paprašė naudotojas.

- Peilio atrama išlaiko įrankio vietinį kontakto tašką ir trumpą 2 cm atleidimo zoną. Vėluojantis jėgos filtras pakeistas tiesioginiu kontakto greičio reguliavimu ir svorio momento kompensavimu. Jėga gali naudoti visą `maximumHydraulicForce`; ankstesni `supportForceMargin` ir `liftForceSurplus` palikti tik serializavimo suderinamumui. `plantedToolSpeed=0.08` m/s, `supportForceSmoothing` dabar nustato kontakto padėties koregavimo laiką. Peilio atramos taškas sveriamas per kontakto plotą, kad mažas posvyris nekeistų visos jėgos iš vieno kampo į kitą. Pašalinta papildoma gravitacijos greičio atsarga, o avarinė persidengimo korekcija sumažinta.
- Stumiamas grunto tūris ribojamas tikru per žingsnį nušluotu sluoksniu, o ne visu išplėstu įrankio tūriu. Peilio šonams tenka 5–20 % vietoj 20–60 %, o kaušo šoninių kraštų gramdymui taikomas atskiras mažas judesio tūrio biudžetas. Gruntas nepanaikinamas: neperkeltas sluoksnis lieka vietoje.
- Rąstas neprijungiamas prie griebtuvo pagal vienos žnyplės kliūtį. Būtini bent du to paties objekto kontaktai priešingomis kryptimis, išlaikytas kontaktas tikroje atšaukto judesio pozoje (6 mm tolerancija) ir 0,08 s patvirtinimas. Į žemę užblokuota žnyplė neleidžia patvirtinti griebimo. `gripContactSkin` ir `gripConfirmTime` derinami Inspector.
- Controls mygtukas atidaro ir uždaro gidą. Kiti meniu pasirinkimai nebeužrakinami; gidas su nepermatomu fonu piešiamas paskutinis ir 3D meniu padedamas prieš konsolę, kad neužstotų Close mygtuko bei nepersidengtų su TRAINING YARD užrašu.

Po atnaujinimo Play sustabdyti ir sceną paleisti iš naujo. Šio papildymo veikimą žaidime dar turi patvirtinti naudotojas.

## Atrama atleidus svirtį

Šis papildymas peržiūrėtas pagal logiką; Unity ir automatiniai bandymai nepaleisti pagal naudotojo ankstesnį prašymą.

- Atleidus valdymą nebeišlaikoma didžiausia kada nors paprašyta kėlimo apkrova. Slėgio tikslas grįžta prie esamo kontakto suspaudimo, o kelių milimetrų kontaktinė paklaida nebekuria naujo kėlimo greičio.
- Įrankiu ir vikšrais stabiliai paremta važiuoklė turi ramybės būseną, atitinkančią uždarytus hidraulinius vožtuvus. Masės centro projekcija turi patekti į tikrų atramų trikampį. Nebūnant valdymo komandos slopinama likusi eiga ir po `supportedRestDelay=0.12` s kūnas užmigdomas; jam nebesiunčiamos vikšrų / įrankio jėgos ir kas žingsnį neperrašomos masės savybės. Ore ar be stabilios atramos tokia būsena neįjungiama.
- Pajudinus svirtis, dar stabdant varikliukui ar vikšrams, pasikeitus įrankio pozai, kroviniui, masių centrui, atramos aukščiui arba įvykus didesniam išoriniam judesiui ramybės būsena nutraukiama.
- `PartDriver` nebeperrašo nepasikeitusios koliderio pozos, kai sąnarys visiškai sustojęs: toks perrašymas galėjo vėl pažadinti važiuoklę kiekviename fizikos žingsnyje.

Rankiniam bandymui po naujo Play paleidimo: nuleisti peilį, pakelti priekinę važiuoklės dalį, visiškai atleisti svirtį ir palaukti; pakartoti atrėmus kaušą. Tada pajudinti svirtį ir patikrinti, kad atramos laikymas iškart atleidžiamas.

## Kabinos sukimas su atrėmtu peiliu ir glaudus rąsto suėmimas

Pakeitimai peržiūrėti pagal kodą; Unity ir automatiniai bandymai nepaleisti.

- Kabinos sukimo komanda nebeatleidžia nejudančio važiuoklės peilio atramos. Judantis masių centras tikrinamas pagal dabartinį atramos plotą, bet vien jo pasislinkimas nebeperjungia važiuoklės į laisvą eigą. Į atramos plotą įtraukti ir abu su gruntu besiliečiantys peilio kraštai. Iš ramybės būsenos išeinant išsaugoma buvusi atramos apkrova, kad pereinant į jėgų valdymą ji nenukristų iki nulio.
- Žnyplių bendrosios apgaubiančios dėžės pakeičiamos iki 16 mažų pasuktų kontaktų vienam tinkleliui, sudarytų iš modelio viršūnių pagal lenktą žnyplės formą. Vidinė išlinkimo ertmė nebeuždaroma viena didele dėže. `cover3.fbx` įjungtas Read/Write duomenų pasiekiamumas šiam vienkartiniam kontaktų paruošimui; žnyplių grunto patikra naudoja retesnius mėginius ant šių mažų kontaktų.
- Atsirėmus į rąstą paliekama iki kontakto leistina uždarymo judesio dalis, o ne atitraukiama per visą kampo žingsnį. Numatyta suėmimo kontakto tolerancija sumažinta iki 2 mm; išliko du priešingi kontaktai ir patvirtinimo laikas.
- Paimtas rąstas tiksliai seka išsaugotą vietinę laikymo pozą. Jo laikinos kolizijos su pačia mašina išjungiamos, kad kinematinis rąstas nestumdytų dinaminės važiuoklės; paleidus atkuriamos ankstesnės porų kolizijos, interpoliacija ir CCD režimas.

Po modelio importo Play paleisti iš naujo. Rankiniu būdu patikrinti abiejų lygių kabinos sukimą su nuleistu peiliu ir skirtingo storio rąstų suėmimą antrame lygyje.

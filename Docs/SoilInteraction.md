# Grunto ir ekskavatoriaus sąveika

## Auditas ir prasilindimo priežastis

Projektas naudoja Unity 6000.6.3f1, URP, XR Interaction Toolkit ir OpenXR su Meta Quest palaikymu. Pirmame lygyje yra kaušas, antrame – rąstų griebtuvas. Antrame lygyje kaušas nepridedamas: bendri pataisymai taikomi strėlei, rankai, griebtuvui, peiliui, važiuoklei ir gruntui.

Dalys valdomos `PartDriver` tiesiogiai keičiant `Transform` kampą; artikuliacijų ar fizinių vyrių grandinės nėra. `ExcavatorBody` tikrina siūlomą pozą. Jo pasaulio kliūčių patikra sąmoningai praleisdavo reljefą, o `BucketInside.GroundPenetration` grąžindavo nulį visam tuščiam kaušui, kol likdavo vietos naujoms žemėms. Todėl turėti TerrainCollider ar įjungti CCD neužteko. Be to, antrame lygyje nėra `BucketInside`, todėl vien kaušo patikra jo įrankio neapsaugojo.

Pradinės scenos turi `TerrainCollider`, dėžinius kaušo / peilio kontaktus, kinematinį pirmo lygio įrankio Rigidbody ir dinaminį antro lygio rąstą. Reljefas keičiamas per TerrainData aukščių žemėlapį. Veikė vietinis šlaitų slinkimas, tūrio apskaita grunto gabalais, kaušo užpildo tinklas, vikšrų atramos, siurblys ir masių modelis.

## Kas pakeista

- Tuščiam kaušui leidžiama tik ribota dantų įpjova (`maximumToothPenetration`, numatyta 0,045 m), tik dirbant tikriems dantims. Kaušo dugnas, pasyvus kritimas, pilnas kaušas ir išnaudotas gabalėlių limitas šios išimties neturi. Judesys skaidomas pagal toliausiai nuo vyrio esantį koliderį, siekiant ne didesnio kaip 8 mm patikros žingsnio.
- Abiejų scenų `ExcavatorContacts` paleidimo metu paruošia judančių dalių primityvius kontaktus. Strėlės ir rankos patikra apima tik jų pačių koliderius, todėl visas vaikiškas kaušo tūris nelaikomas vientisu strėlės bloku. Esami kaušo apvalkalo kontaktai išlaikomi. Griebtuvo žnyplės tikrinamos ir atidarant, ir uždarant; iš jau įvykusio persidengimo galima ištraukti įrankį.
- Kinematiniams mašinos Rigidbody naudojamas `ContinuousSpeculative`, interpolacija ir vietiniai 10/4 solverio žingsniai. Krentantis gruntas lieka `ContinuousDynamic`. Bendri solverio nustatymai – 8/2, didžiausias išstūmimo greitis – 2 m/s. Fiksuotas žingsnis paliktas 0,02 s: judesio saugą užtikrina papildomi pozos patikros žingsniai, o ne visos fizikos dažnio didinimas.
- Kaušas turi kubiniais metrais išreikštą talpą ir serializuotą esamą tūrį. `ratedCapacityCubicMetres = 0` automatiškai kalibruoja iš esamos krovinio dėžės ir senos gabalų talpos. `heapedFraction = 0,15` leidžia rodyti iki maždaug 115 % nominalios talpos. Nominali talpa visada ribojama geometrine krovinio erdve; didelis skaičius Inspector lange nesukuria neriboto kaušo.
- Užpildo tinklas normalizuojamas pagal laikomą tūrį. Vidurinė dalis gali iškilti virš krašto, kraštai lieka žemiau. Pasvirimas perstumia užpildą, išpylimas mažina jį palaipsniui. Paslėptiems apskaitos gabalams nebeieškoma atsitiktinių nesikertančių pozicijų ir nebekuriami laikini pozicijų masyvai.
- Numatyta išpylimo riba – 0,014 m³/s ir daugiausia 32 laisvi dinaminiai grunto gabalai. Pasiekus ribą likęs tūris pasilieka kauše; atsilaisvinus vietai išpylimas tęsiamas be sukaupto vieno kadro pliūpsnio. Laikomi gabalai yra kinematiniai, nematomi, išjungtais kolideriais. Esamas objektų fondas naudojamas pakartotinai.
- `Assets/Soil` yra keturi redaguojami `SoilMaterialProfile` ištekliai: Sand, Loose dirt, Clay, Compacted. Jie valdo tankį, kasimo pasipriešinimą, birėjimo kampą, sankibą, trintį ir minkštumą. Išpiltų gabalų grunto rūšis išsaugoma grubiuose medžiagos blokuose; tai nėra smulkių atskirų sluoksnių ar skirtingų medžiagų mišinių simuliacija.
- Didesnis įgilinimas ir greitis didina pjovimo pasipriešinimą. Krovinio masė mažina hidraulinį greitį bei pagreitį. Peilio apkrova priklauso nuo stumiamo tūrio ir medžiagos; ji lėtina važiavimą. Žemės perkeliamos į priekinį pylimą ir aplink peilio galus, išlaikant tūrį.
- Hidraulinis kėlimas tikrinamas pagal svorio momentą apie priešingą vikšrų atramą. Jėga turi viršyti svorio, masių centro ir įrankio peties nustatytą slenkstį. Mašina sukama apie atramą, o ne visa perkeliama aukštyn. Posvyris ir greitis riboti, nusėdimas slopinamas.
- Vienas `SoilWorkFeedback` emiteris aptarnauja kasimą, stūmimą ir išpylimą: iki 128 dulkių dalelių, erdvinis žemių šiurenimas ir pirmo lygio pasaulinis talpos rodmuo kabinoje. Garsui galima priskirti savo `soilLoop`; be jo naudojamas sukurtas triukšmo ciklas. Hidraulinė apkrova perduoda silpnus trumpus impulsus tik laikomam valdikliui, kartu išlaikant svirties galinės padėties vibraciją.

## VR apkrovos ribos

`TerrainSoilCache` saugo dabartinius aukščius ir pakartotinai naudoja redagavimo langus. Po apšilimo pasikartojantiems langų dydžiams nekuriami nauji aukščių / pjūvio masyvai. Persidengiantys stūmimo ir supylimo langai taiko aukščio skirtumus, todėl vienas negali užrašyti kito ką tik supiltos žemės. Išoriniai TerrainData pakeitimai invaliduoja talpyklą.

Kontaktai ir pradinis reljefas interpoliuojami pagal tuos pačius langelio trikampius kaip Unity Terrain. Bilinearinis keturkampio vidurkis ties aštria iškasos briauna davė kitą aukštį; tai galėjo sukelti klaidingą atramą ar nudažyti nepakeistą vietą. Dažymas lygina vaizduojamą reljefą su taip pat interpoliuotu pradiniu paviršiumi.

Šlaitų simuliacija atnaujina numatytus du 16 langelių blokus per ciklą; už 30 m nuo stebėtojo esantys blokai atidedami. Aukščiai ir reljefo kolideris sinchronizuojami kas 0,08 s, o kontaktai tuo metu jau mato naujausius aukščius. Dažymas apdoroja du 32×32 blokus per ciklą naudodamas vieną masyvą. Smulkių išpylimo grūdelių limitas sumažintas iki 600, jų kolizijos nenaudoja dinaminių kūnų.

Tai lengvas kinematinės hidraulikos ir aukščių žemėlapio modelis, ne birių dalelių/FEM ar visiškai dinaminė ArticulationBody mašina. VR FPS dar nematuotas su Quest 2; 72–90 FPS nėra garantuojamas vien šiomis automatinėmis patikromis. Matuoti reikia tikrame build'e, ne vien redaktoriuje.

## Derinimas ir patikra Play režimu

1. **Abu lygiai, kontaktai:** užkurti mašiną, nuleisti įrankį ir strėlę į žemę, bandyti greitai sukti abiem kryptimis. Įrankis turi sustoti arba kasti / stumti tik leistiną sluoksnį. Išjungus variklį pasyvus judesys neturi kasti. Antrame lygyje patikrinti ir žnyplių atidarymą prie grunto.
2. **Pirmas lygis, krovinys:** dantimis kasti pirmyn ir užlenkti kaušą. Kabinos rodmuo turi didėti, viršutinė užpildo dalis – sudaryti kauburį. Pakelti, važiuoti ir pasukti: krovinys neturi iškristi pro sieneles. Greitai išversti: turinys turi mažėti kartu su srautu, o reljefas pakilti nusileidimo vietoje.
3. **Pylimas ir medžiagos:** palyginti Sand ir Clay profilius ar `SoilRegion.kind` zonas. Smėlis turi lengviau kastis ir labiau slinkti; molis – labiau priešintis ir ilgiau laikytis kauše. Tikrinti mažą duobę ir pylimą: neturi pakisti tolimas nejudintas reljefas.
4. **Peilis:** nuleisti iki grunto ir važiuoti pirmyn. Turi formuotis pylimas, sunkėjant gruntui mažėti greitis. Pakėlus peilį normali eiga turi grįžti. Bandymas stipriai spausti peilį žemyn turi kelti atitinkamą važiuoklės kraštą.
5. **Atrama kaušu:** dantų / dugno atramą pastatyti ant žemės ir spausti strėle žemyn. Priešingas vikšrų kraštas turi likti atramoje. Pakėlus įrankį važiuoklė turi nusėsti. Derinti `maximumHydraulicForce` (18 000 N), `liftThreshold` (1,1), `maxHydraulicTilt` (10°), `hydraulicTiltSpeed` ir `settlingDamping` (0,25 s). Masės ir bazinis masių centro poslinkis yra `ExcavatorMass`.
6. **VR našumas:** stebėti CPU Physics, skriptus, GC ir GPU kasant, sukant bei ilgai pilant. Pirmiausia mažinti `maxLooseBodies`, `soilTilesPerStep`, `paintTilesPerFlush` ir dulkių / smulkių grūdelių limitus. Haptikos stiprumas – `LeverTilt.hapticStrength`; efektų atstumas ir garsumas – `SoilWorkFeedback`.

Automatinės patikros: `RealisticControlValidation.Run` tikrina ankstesnį valdymą, kaušą, tūrį, peilį, garsą, mases ir važiavimą; `SoilInteractionValidation.Run` – naujus kontaktų saugiklius, kauburį, fizinių gabalų limitą, talpyklos atnaujinimą, jėgos slenkstį ir tikrų scenų integraciją. Abu metodai skirti atskiram Unity batch procesui ir jį užbaigia.

Pakeistos scenos: `Assets/Scenes/Level 1.unity`, `Assets/Scenes/Level 2.unity`. Meniu ir naudotojo vietiniai jo pakeitimai į šį pataisymą neįtraukiami. Esami žaidimo prefabai neperrašyti; bendri komponentai konfigūruojami scenose ir paleidžiant žaidimą.

Audituotas abiejų scenų grunto prefab'as – `Assets/Frefabs/Cube.prefab`: jis jau turėjo dinaminį Rigidbody su Continuous Dynamic ir dėžinį koliderį. `DirtBlock` aktyvavimo metu parenka netaisyklingą vizualų tinklą, rutulinį kontaktą ir masę pagal tūrio bei medžiagos tankio sandaugą; prefab'o failas nekeistas.

Skriptų pakeitimų sąrašas (`Assets/Scripts`):

| Sistema | Failai |
|---|---|
| Kontaktai ir judesys | `ExcavatorContacts.cs` (naujas), `ExcavatorBody.cs`, `GroundedAttachment.cs`, `PartDriver.cs`, `Claw.cs` |
| Kaušas ir gabalai | `BucketInside.cs`, `BucketSoilFill.cs`, `DigTeeth.cs`, `DirtBlock.cs` |
| Reljefas ir medžiagos | `DigSystem.cs`, `SoilDynamics.cs`, `TerrainSoilCache.cs` (naujas), `SoilMaterialProfile.cs` (naujas) |
| Apkrova ir atrama | `ExcavatorGrounding.cs`, `ExcavatorHydraulics.cs`, `ExcavatorMass.cs`, `TrackDrive.cs` |
| Efektai | `SoilWorkFeedback.cs` (naujas), `SandFlow.cs`, `LeverTilt.cs` |

Taip pat atnaujinti `ProjectSettings/DynamicsManager.asset`, `Assets/Editor/AdvancedSoilValidation.cs`, `BucketGroundValidation.cs`, `RealisticControlValidation.cs`; pridėtas `Assets/Editor/SoilInteractionValidation.cs`, keturi `Assets/Soil/*.asset` profiliai ir jų `.meta` failai.

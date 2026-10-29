# Ekskavatoriaus ir grunto fizika

Abiejuose lygiuose įjungtas bendras siurblys (`ExcavatorHydraulics`), judančių dalių svoris (`ExcavatorMass`), reljefo atramos ir vietinė grunto dinamika (`DigSystem`). Tai valdomas heightmap modelis; iškasos neturi urvų ar požeminių ertmių.

- **Dantys:** `DigTeeth.toothCount` paskirsto atskirus pjūvius per tikrą kaušo plotį. Dantų pradžios ir pabaigos pozicijos fiksuojamos prieš kiekvieną leidžiamą judesį. Kaušo dugnas bei pasyvus kritimas nekasa.
- **Gruntas:** pradžioje priemolis; nuo 0,25 m – molis, nuo 0,7 m – suspaustas sluoksnis. `SoilRegion` leidžia Inspector lange pasirinkti kitą gruntą ir apibrėžti jo tūrį be fizinio koliderio. Birios supiltos žemės slenka lengviau. Keičiasi pjovimo pasipriešinimas, sukibimas, tūrinis tankis, laikymasis kauše ir šlaito stabilumas.
- **Slinkimas:** skaičiuojamos tik pajudintos reljefo sritys. Gretimų heightmap taškų tūris perduodamas poromis, todėl nuo šlaitų slenkanti žemė nedingsta. `soilTilesPerStep` riboja darbą viename žingsnyje; smulkumą riboja reljefo heightmap raiška.
- **Krovinys:** kauše esantis tūris atitinka užpildo geometriją. Užpildas pasislenka bei pasvyra pagal gravitaciją. Pakreipus išbyra tik tiek, kiek nebeišsilaiko; molis laikosi ilgiau. Perteklinis krovinys išleidžiamas per kraštą.
- **Hidraulika:** vienos mašinos vožtuvai dalijasi `pumpCapacity`. Ištiestą strėlę bei sunkų gruntą riboja `maximumPressure` ir svirties petys. Atsirėmęs įrankis gali ribotai pakelti važiuoklę; visų įrankių ir vikšrų kontaktai lieka tikrinami.
- **Svoris:** masių centras seka strėlę, ranką, įrankį ir laikomas žemes. Nuleistas peilis praplečia atramos plotą. Priekyje arba šone padidėjusi apkrova keičia grunto slėgį ir provėžas; artėjant atramos ribai važiavimas lėtėja.
- **Vikšrai:** kiekviena pusė turi savo sukibimą ir praslydimą. Grandinių greitis gali skirtis nuo važiuoklės greičio. Minkštame grunte žemės išstumiamos į provėžų šonus; gylis ribotas, tūris išsaugomas.
- **Peilis:** prieš peilį formuojamas žemių pylimas, dalis medžiagos išspaudžiama per abu jo galus. Augant apkrovai didėja pasipriešinimas. Pylimas vėliau nusėda pagal grunto savybes.

## Patikra

Unity Editor patikras paleidžia `RealisticControlValidation.Run`. Papildomi `AdvancedSoilValidation` scenarijai tikrina skirtingo krašto pjūvį, medžiagų pasipriešinimą, nuošliaužų tūrį, dalinį išbyrėjimą, bendrą siurblį, tikrų scenų mases, peilio atramą, praslydimą, provėžas ir išstūmimą per peilio galus. VR pojūtį ir našumą reikia papildomai įvertinti pačiuose akiniuose.

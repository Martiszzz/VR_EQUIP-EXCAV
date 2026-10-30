# Erdvinis VR meniu

Paleidus `Menu` sceną sukuriama 3D darbų aikštelė: du tikri lygių ekskavatoriai, betono ir grunto danga, fiziniai pasirinkimo stendai, valdymo pultas, aptvarai, konteineriai ir kūgiai. Tai erdvinė aplinka su perspektyva ir paralaksu, o ne visą vaizdą uždengiantis skydelis.

Visi žaidėjui rodomi meniu tekstai yra anglų kalba, įskaitant 3D stendus, atmintinę, įkėlimo būseną ir klaidas.

## Peržiūra redaktoriuje be Play

Pasirink `Tools > Excavator > Preview 3D Menu`. Arba pažymėk `MenuController` ir jo Inspector lange spausk `Preview 3D Menu`. Atsidaro laikina 3D aikštelės peržiūra `Scene` lange. Galima laisvai sukti kamerą ir apžiūrėti modelius. Atmintinė rodoma per `Tools > Excavator > Preview Controls Guide`.

Grįžk viršuje esančia etapo grįžimo rodykle arba `Tools > Excavator > Back to Scene`. Peržiūra neįrašoma į `Menu` ar kitą redaguojamą sceną ir automatiškai uždaroma prieš paleidžiant žaidimą.

## Pasirinkimas

Galima nukreipti VR spindulį į patį ekskavatorių ir naudoti XR pasirinkimo veiksmą arba spausti jo stendo mygtuką. Abu keliai naudoja tą patį esamą lygio `OnClick` veiksmą. Pasirinkta darbo zona paryškinama apšvietimu. Kol atverta atmintinė ar įkeliamas lygis, pradėjimo veiksmai užblokuoti.

`ExcavatorMenuPresentation.immersiveYard` įjungtas meniu scenoje. Aikštelė vieną kartą orientuojama pagal pradinę žaidėjo žiūrėjimo kryptį ir lieka pasaulyje; ji neseka galvos judesių. `bucketShowcase` ir `grappleShowcase` nurodo tikrus demonstracinius modelius. Valdymo atmintinė pateikiama atskirame artimesniame skydelyje. `menuSounds` išjungia pasirinkimo garsus ir tylų erdvinį aikštelės variklio foną.

## Modeliai ir našumas

`Assets/Menu` modeliai paruošti pagal tikras `Level 1` ir `Level 2` geometrijos pozicijas. Jie neturi kasimo, važiavimo ar variklio valdymo skriptų. Medžiagų dalys sujungtos į 8 ir 7 tinklus, o nenaudojamos viršūnės pašalintos. `MenuShowcaseBuilder.Run` gali perkurti šiuos demonstracinius prefabus po modelių pakeitimų; metodas skirtas atskiram Unity batch procesui ir jį užbaigia.

## Patikros

`ImmersiveMenuValidation.Run` tikrina tikrus modelius, skirtingą stendų gylį ir kampą, spindulio paspaudimus, pasirinkimą per 3D objektą, apšvietimo reakciją, atmintinę, blokavimą įkeliant ir scenų nuorodas. Taip pat generuojamos peržiūros `C:/Temp/VR-menu-yard.png`, `VR-menu-yard-side.png`, `VR-menu-yard-help.png` ir `VR-menu-yard-loading.png`. Renderinant Unity paleidžiamas su grafika, be `-nographics`. Galutinį VR pojūtį ir našumą reikia įvertinti su akiniais.

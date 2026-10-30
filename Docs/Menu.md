# Erdvinis VR meniu

`Menu` scenoje išsaugota 3D darbų aikštelė: du tikri lygių ekskavatoriai, betono ir grunto danga, fiziniai pasirinkimo stendai, valdymo pultas, aptvarai, konteineriai ir kūgiai. Tai erdvinė aplinka su perspektyva ir paralaksu, o ne visą vaizdą uždengiantis skydelis.

Visi žaidėjui rodomi meniu tekstai yra anglų kalba, įskaitant 3D stendus, atmintinę, įkėlimo būseną ir klaidas.

## Redagavimas Unity redaktoriuje

Atidaryk Assets/Scenes/Menu.unity. Visa aikštelė išsaugota po 3D Menu objektu ir iš karto matoma Scene lange bei Hierarchy: modeliai, stendai, tekstai, valdymo pultas ir apšvietimas. Play ar atskiros peržiūros nereikia. Objektų pozicijas ir tekstus galima keisti įprastai, po to išsaugoti sceną.

Procedūrinė geometrija, medžiagos ir šrifto atlasas išsaugoti Assets/Menu/SceneAssets. Paleidus žaidimą prijungiami mygtukų ir XR pasirinkimo veiksmai, o išsaugota aikštelė nebekuriama iš naujo. Atmintinės ir įkėlimo skydeliai scenoje išsaugoti neaktyvūs.

## Pasirinkimas

Galima nukreipti VR spindulį į patį ekskavatorių ir naudoti XR pasirinkimo veiksmą arba spausti jo stendo mygtuką. Abu keliai naudoja tą patį esamą lygio `OnClick` veiksmą. Pasirinkta darbo zona paryškinama apšvietimu. Kol atverta atmintinė ar įkeliamas lygis, pradėjimo veiksmai užblokuoti.

`ExcavatorMenuPresentation.immersiveYard` įjungtas meniu scenoje. Aikštelė naudoja scenoje išsaugotas pozicijas ir nesisuka kartu su žaidėjo galva. `bucketShowcase` ir `grappleShowcase` nurodo tikrus demonstracinius modelius. Valdymo atmintinė pateikiama atskirame artimesniame skydelyje. `menuSounds` išjungia pasirinkimo garsus ir tylų erdvinį aikštelės variklio foną.

## Modeliai ir našumas

`Assets/Menu` modeliai paruošti pagal tikras `Level 1` ir `Level 2` geometrijos pozicijas. Jie neturi kasimo, važiavimo ar variklio valdymo skriptų. Medžiagų dalys sujungtos į 8 ir 7 tinklus, o nenaudojamos viršūnės pašalintos. `MenuShowcaseBuilder.Run` gali perkurti šiuos demonstracinius prefabus po modelių pakeitimų; metodas skirtas atskiram Unity batch procesui ir jį užbaigia.

## Patikros

`ImmersiveMenuValidation.Run` tikrina tikrus modelius, skirtingą stendų gylį ir kampą, spindulio paspaudimus, pasirinkimą per 3D objektą, apšvietimo reakciją, atmintinę, blokavimą įkeliant ir scenų nuorodas. Taip pat generuojamos peržiūros `C:/Temp/VR-menu-yard.png`, `VR-menu-yard-side.png`, `VR-menu-yard-help.png` ir `VR-menu-yard-loading.png`. Renderinant Unity paleidžiamas su grafika, be `-nographics`. Galutinį VR pojūtį ir našumą reikia įvertinti su akiniais.

SavedMenuValidation.Run tikrina iš naujo atidarytą išsaugotą sceną, išteklių nuorodas, lygių pasirinkimo įvykius, atmintinę ir tai, kad prijungiant valdymą nedubliuojami objektai. MenuSceneBaker.Run yra scenos autoriavimo priemonė atskiram Unity batch procesui: eksportuoja naują aikštelę į Assets/Menu/LayoutExport.unity ir užbaigia procesą. Įprastam redagavimui jos nereikia.

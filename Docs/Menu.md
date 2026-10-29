# Pagrindinis meniu

Naujas meniu sukuriamas paleidus `Menu` sceną. `ExcavatorMenuPresentation` prijungtas prie `MenuController`: jis naudoja esamus dviejų lygių mygtukus ir išsaugo jų `OnClick` scenų nuorodas. Scenos XR rig, įvesties modulis ir `TrackedDeviceGraphicRaycaster` lieka prijungti.

Meniu kortelės, valdymo atmintinė ir tikro asinchroninio įkėlimo eiga pateikiamos lietuviškai. `Inter` šriftui sukuriamas dinaminis TMP atlasas su reikalingais simboliais. Iliustracijos yra lengva vektorinė UI geometrija. Pasirinkimo garsai sintezuojami vieną kartą; juos galima išjungti per `menuSounds`.

VR skydas yra 1,76 m pločio ir pirmą kartą išdėstomas maždaug 2,3 m prieš žaidėją. Jis neseka kiekvieno galvos judesio. Atstumas keičiamas per `viewingDistance`. Pasirinkimas vyksta esamu valdiklio spinduliu ir UI paspaudimu. Klaviatūros Escape uždaro valdymo atmintinę.

`MenuPresentationValidation.Run` tikrina, ar išlieka mygtukų veiksmai, nesidubliuoja UI ir garsai, telpa tekstai, yra lietuviški simboliai, veikia fokusas, atmintinė ir įkėlimo būsenos. Peržiūros įrašomos į `C:/Temp/VR-menu-main.png`, `VR-menu-focus.png`, `VR-menu-help.png` ir `VR-menu-loading.png`. Renderinant šias peržiūras Unity turi būti paleistas su grafika, be `-nographics`.

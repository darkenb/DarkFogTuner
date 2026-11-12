# DarkFogTuner

Dark Fog zorluğunu oyun oynarken, oyunun kendi pencere görünümüyle değiştirir. Panel yalnız oyundayken açılır, Esc menüsünde ve ana menüde kapalıdır. Arayüz İngilizcedir. Oyundayken `Ctrl+Shift+K` ile paneli açın, kaydırıcıyı oynatıp fareyi bırakın; ayar o anda uygulanır. Kolay, Normal, Zor ve Aşırı düğmeleri her şeyi birden ayarlar. Kısayol `BepInEx/config/bug.dsp.darkfogtuner.cfg` dosyasındadır.

Her kaydırıcıda Normal değerini gösteren bir işaret vardır. Çubuk bu işaretten başlar: turuncu Normal'den zor, mavi Normal'den kolay demektir. Normal'deki değerler soluk görünür, üzerinde olduğunuz ön ayarın düğmesi vurgulanır.

| Ayar | Aralık | Ne yapar |
|---|---|---|
| Saldırganlık | Kukla - Öfkeli | Oyunun altı kademesi |
| Saldırı sıklığı | ×0,1 - ×100 | Kovan ve yer üslerinde saldırı için gereken tehdit |
| Filo büyüklüğü | ×0,1 - ×20 | Saldırı başına birim sayısı |
| Filo büyümesi | 0 - ×10 | Saldırıların dalga dalga büyüme hızı; 0 büyümeyi durdurur |
| Diğer yıldızlara yayılma | 0 - ×20 | Tohumların fırlatılma olasılığı; 0 yeni fırlatmaları durdurur |
| İnşa ve üretim hızı | ×0,1 - ×10 | Kovan ve üs inşası, birim üretimi |
| Enerji tehdidi, savaş tehdidi | ×0,01 - ×100 | Oyunun tehdit çarpanları. Enerji tehdidi yalnız yer üslerini besler; Dyson küresinin uzay tehdidi saldırı sıklığına bağlıdır |
| Savaştan deneyim | ×0,01 - ×20 | Oyunun deneyim çarpanı |
| Birim kapasitesi | ×1 - ×20 | Kovan veya üssün tutabileceği birim sayısı. Uzay saldırısı tavanını da 1440'a kadar yükseltir |
| Bedava birim üretimi | ×1 - ×50 | Tik başına ek üretim turu; kovandan madde veya enerji harcamaz |
| Düşman hasarı | ×0,1 - ×100 | Binalara, mekaya ve gezegen kalkanlarına verilen hasar |
| Düşman dayanıklılığı | ×0,1 - ×100 | Kara Sis birim ve yapılarının aldığı hasarı böler. 30. seviyenin üstüne eklenir |
| Yeni kovan seviyesi, başlangıç avantajı | 0 - 30, 0 - 4 | Yalnız bundan sonra kurulan kovanlar |
| Tehdit seviyesi | %0 - %100 | Kaydırıcıyı bıraktığınızda bütün kovanların ve yer üslerinin saldırı göstergesini bir kez bu değere getirir; %100 saldırıları başlatır. En yüksek göstergeyi gösterir. Saklanmaz; Pasif ve altı seviyeler yine saldırmaz |

Oyunun kendi ayarları kayıt dosyasında saklanır. Saldırı sıklığı, filo büyüklüğü, filo büyümesi, yayılma, birim kapasitesi, üretim, düşman hasarı ve dayanıklılığı ise config dosyasında galaksi başına saklanır; aynı galaksinin kayıtları bu değerleri paylaşır.

## They Come From Void (DSP_Battle)

Bu modla birlikte yükleme, kaydetme ve uzay saldırıları sorunsuz çalışır. İki mod aynı hasar metotlarını yamaladığı için etkileri beklenmedik şekilde birleşebilir; örneğin kalıntı hasar bonusları veya kalkanlar farklı davranabilir. Böyle bir şey görürseniz ikisini ×1'e alıp [bildirin](https://github.com/darkenb/DarkFogTuner/issues).

## Sınırlar

- Kovan sayısı ve başlangıçtaki kovanlar yeni oyun ayarlarından üretilir, sonradan değişmez; bunlar için yeni oyun ekranında UniverseGenTweaks kullanın.
- Saldırı yalnız var olan birimleri kullanabilir; filo büyüklüğünün görünmesi için kapasite ve üretim de gerekir. Uzay saldırısı kovan başına 1440, yer saldırısı üs başına 180 birimde durur (oyunun birim yuvaları).
- Çok yüksek kapasite ve üretim kare hızını düşürebilir.
- Yayılma yalnız başka yıldızlara giden tohumları kapsar. Aynı sistemdeki gezegenlere inişler inşa hızına bağlıdır.
- Zorluğu düşürmek oyunun kendi değişikliği gibi kaydedilir, yani meta veri ulaşılan en düşük değere göre hesaplanır. Oyunun kendi değişikliğinden farklı olarak meta veri bedeli alınmaz.
- İnşa hızı ve savaş tehdidi kayıttaki oyun tanımına da yazılır. Çok oyunculu için yapılmadı. Mod kaldırılırsa kayıttaki oyun ayarları kalır; saldırı eşikleri her kovanın bir sonraki saldırısından sonra normale döner.

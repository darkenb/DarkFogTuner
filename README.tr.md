# DarkFogTuner

Dark Fog zorluğunu oyun oynarken değiştirir. Oyundayken `Ctrl+Shift+K` ile paneli açın, kaydırıcıyı oynatıp fareyi bırakın; ayar o anda uygulanır. Kolay, Normal, Zor ve Aşırı düğmeleri her şeyi birden ayarlar. Kısayol ve dil `BepInEx/config/bug.dsp.darkfogtuner.cfg` dosyasındadır.

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

Oyunun kendi ayarları kayıt dosyasında saklanır. Saldırı sıklığı, filo büyüklüğü, filo büyümesi, yayılma, birim kapasitesi, üretim, düşman hasarı ve dayanıklılığı ise config dosyasında galaksi başına saklanır; aynı galaksinin kayıtları bu değerleri paylaşır.

## Sınırlar

- Kovan sayısı ve başlangıçtaki kovanlar yeni oyun ayarlarından üretilir, sonradan değişmez; bunlar için yeni oyun ekranında UniverseGenTweaks kullanın.
- Saldırı yalnız var olan birimleri kullanabilir; filo büyüklüğünün görünmesi için kapasite ve üretim de gerekir. Uzay saldırısı kovan başına 1440, yer saldırısı üs başına 180 birimde durur (oyunun birim yuvaları).
- Çok yüksek kapasite ve üretim kare hızını düşürebilir.
- Yayılma yalnız başka yıldızlara giden tohumları kapsar. Aynı sistemdeki gezegenlere inişler inşa hızına bağlıdır.
- Zorluğu düşürmek oyunun kendi değişikliği gibi kaydedilir, yani meta veri ulaşılan en düşük değere göre hesaplanır. Oyunun kendi değişikliğinden farklı olarak meta veri bedeli alınmaz.
- İnşa hızı ve savaş tehdidi kayıttaki oyun tanımına da yazılır. Çok oyunculu için yapılmadı. Mod kaldırılırsa kayıttaki oyun ayarları kalır; saldırı eşikleri her kovanın bir sonraki saldırısından sonra normale döner.

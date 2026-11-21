# Değişiklikler

## v1.0.1

GitHub'daki v1.0.0 kaynak kodu temel alınarak hazırlanmıştır.

### Düzeltmeler

- Insert, yalnızca 25 ms aralıklarla tuş durumu okunarak değil, Windows
  `RegisterHotKey` / `WM_HOTKEY` mekanizmasıyla yakalanır.
- Pencere görünür işaretli olsa bile oyun tarafından örtülmüşse Insert artık
  onu gizlemek yerine geri getirir, en üst sıraya alır ve etkinleştirir.
- Aynı tuşun hem global kısayol hem zamanlayıcı yoluyla iki kez işlenmesi önlenir.
- `MOD_NOREPEAT` ile basılı tutulan Insert'in tekrar tekrar aç/kapat yapması engellenir.
- Küçültülmüş pencere geri yüklenir; ekran dışında kalmış pencere yeniden konumlandırılır.
- `--hidden` başlangıcında ilk Insert isteğinin başlangıç gizlemesiyle kaybolması önlenir.
- EXE yeniden çalıştırıldığında mevcut pencere bir yerel sinyalle gösterilir.
- Global Insert kaydı başka uygulamayla çakışırsa günlük bildirimi ve yedek
  tuş algılama yolu kullanılır.

### Doğrulama

- Önceki GitHub v1.0.0 EXE'siyle örtülmüş pencere hatası yeniden üretildi.
- Yeni sürüm, kenarlıksız tam ekran test penceresiyle 7 entegrasyon senaryosunu geçti.
- Pencere görünürlüğü kararları ve mevcut x64 mühimmat/sağlık kodu testleri geçti.
- Gerçek Mafia oyunu içindeki exclusive tam ekran davranışı ve hilelerin
  oyun içi etkileri bu sürümde test edilmedi.

### Dağıtım

- Tek dosya, kurulum gerektirmeyen Windows x64 `MafiaDE.exe`.
- Orijinal ikon, ikonla uyumlu tema ve mevcut oyun sürümü kontrolü korunur.
- Kaynak kod, portable EXE ve kaynak ZIP'i kişisel bilgi / yerel yol taramasından geçirilir.

## v1.0.0

- İlk açık kaynak ve portable sürüm.
- Türkçe koyu tema, orijinal oyun ikonu, F1 mühimmat ve F2 sağlık seçenekleri.
- Desteklenen Epic Games EXE'si için dosya özeti ve tekil bellek imzası kontrolleri.

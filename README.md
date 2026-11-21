# MafiaDE

**Mafia: Definitive Edition için Türkçe, açık kaynak ve tek dosya portable trainer.**

MafiaDE; mühimmat ve sağlık seçeneklerini sade bir masaüstü arayüzünde sunar.
Orijinal oyun ikonu, ikonun turuncu-kızıl / siyah / kırık beyaz renkleriyle
hazırlanmış tema ve oyun açıkken kullanılabilen klavye kısayolları içerir.
Kurulum, ayrıca .NET yüklemesi veya Cheat Engine gerektirmez.

**[Portable EXE'yi indir](https://github.com/sametyamanxx/MafiaDE/releases/latest/download/MafiaDE.exe)**
· [Tüm sürümler](https://github.com/sametyamanxx/MafiaDE/releases)
· [Kaynak kod](https://github.com/sametyamanxx/MafiaDE)

> **Durum:** EXE ve bellek imzaları doğrulandı; x64 kodu sentetik verilerle
> test edildi. Sağlık ve mühimmat seçeneklerinin gerçek oyun içi etkisi henüz
> test edilmedi. Yalnızca aşağıdaki desteklenen oyun sürümünde etkinleştirilir.

## Özellikler

| Özellik | Açıklama |
| --- | --- |
| Sınırsız mühimmat | Yedek mühimmatı 99'a tamamlamayı hedefler; şarjör değiştirme devam eder. |
| Sınırsız sağlık | Sağlık okunduğunda değeri maksimum sağlığa tamamlamayı hedefler. |
| Otomatik bağlantı | Oyun sürecini algılar, dosya sürümünü ve bellek imzalarını kontrol eder. |
| Orijinal ikon ve uyumlu tema | Oyunun ikonunu tüm gömülü çözünürlükleriyle korur. |
| Portable kullanım | Windows x64 üzerinde tek EXE; kurulum ve ek dosya gerektirmez. |
| Çevrimdışı çalışma | Telemetri, otomatik indirme, başlangıç kaydı veya servis içermez. |

## Kısayollar

| Tuş | İşlev |
| --- | --- |
| **Insert** | Pencereyi göster / gizle |
| **F1** | Sınırsız mühimmatı aç / kapat |
| **F2** | Sınırsız sağlığı aç / kapat |

## Kullanım

1. Releases bölümündeki `MafiaDE.exe` dosyasını indir ve çalıştır.
2. Mafia: Definitive Edition'ı aç. Uygulama oyunu otomatik bulur.
3. **Insert:** pencereyi göster / gizle.
4. **F1:** sınırsız mühimmat aç / kapat (yedek mühimmat 99'a tamamlanır; şarjör değiştirme devam eder).
5. **F2:** sınırsız sağlık aç / kapat (sağlık okunduğunda maksimum değere tamamlanır).

F1/F2 yalnızca oyun veya MafiaDE ön plandayken işlenir. Insert globaldir.
Küçült düğmesi sistem tepsisine gizler; kırmızı çarpı uygulamadan çıkar ve
uygulanan kod yamalarını geri alır. Sistem tepsisindeki ikona çift tıklayarak
veya sağ tık menüsünden de pencereyi açabilirsin.

Program gizliyken başlatmak için: `MafiaDE.exe --hidden`.

Oyun yönetici olarak çalışıyorsa MafiaDE'yi de yönetici olarak çalıştır.
Başka trainer / Cheat Engine yamalarını aynı anda kullanma.
Tam ekranın üstünde görünmüyorsa oyunu kenarlıksız pencere moduna al.
Bu bir masaüstü penceresidir, oyun içine enjekte edilen görsel overlay değildir.

## Destek ve doğrulama durumu

- Epic Games kurulumu, EXE dosya sürümü `1.0.0.1`.
- Desteklenen SHA-256:
  `A98A2B22FB966E6F617B9ADB69DA4F2EB75EC794750C03AA3E23B2F6F29C76E1`.
- EXE dosya özeti ve çalışan oyundaki tekil bellek imzaları denetlenmeden
  hiçbir yama uygulanmaz. Farklı sürümler bilerek engellenir.
- **Bellek imzaları desteklenen EXE'de doğrulandı. Üretilen x64 kodu sentetik verilerle
  test edildi. Gerçek oyun içi sağlık/mühimmat etkisi henüz test edilmedi.**
  İmza eşleşmesi tek başına oyun içi etkiyi garanti etmez.
- Sağlık güncelleme noktasına bağlı olarak anlık ölüm, senaryolu ölüm, araç hasarı
  veya ara sahne hasarı engellenmeyebilir. Yedek kaydını tut.
- Oyun dosyaları veya kayıtlar program tarafından düzenlenmez. Yalnızca çalışan
  oyunun belleğinde iki özellik için geçici yama uygulanır.
- Kapanışta yamalar geri alınır. Devam eden bir komuta geçersiz adres bırakmamak
  için kullanılan küçük kod blokları oyun kapanana kadar bellekte tutulur
  (özellik başına 4 KB).
- Ağ bağlantısı, otomatik indirme, telemetri, servis, başlangıç kaydı veya kurulum yoktur.
  .NET tek dosya çalışma zamanı gerektiğinde Windows geçici klasörüne açılır;
  portable EXE başka bir dosyaya veya sabit oyun kurulum yoluna ihtiyaç duymaz.

## Derleme

.NET 8 SDK ve Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1
```

Çıktı: `portable\MafiaDE.exe` (Windows x64, self-contained, tek dosya).
İlk derlemede .NET çalışma zamanı paketlerinin indirilmesi için internet gerekebilir.

İkonu kendi yasal oyun kurulumundan yeniden çıkarmak istersen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Extract-Icon.ps1 -GamePath "OYUN_KLASORU\mafiadefinitiveedition.exe"
```

Projede bilgisayar kullanıcı adı veya sabit kurulum yolu bulunmaz. Hata ayıklama
sembolleri yayımlanmaz; derleme kaynak yolları nötr bir yola eşlenir.

Test:

```powershell
Start-Process .\portable\MafiaDE.exe -ArgumentList '--self-test --test-report test-results.txt' -Wait
Get-Content .\test-results.txt
```

## Kaynak notu

İki bellek imzasının ve alanların araştırma kaynağı:
[l0wb1t — Mafia: Definitive Edition, table v0.1b](https://fearlessrevolution.com/viewtopic.php?t=13919).
Cheat Engine veya indirilen tablo çalışma zamanında kullanılmaz; orijinal tablo
projeye dahil edilmemiştir. Sağlık etkisi sabit 1000 yerine maksimum sağlığı kullanır.
## Lisans ve sorumluluk

Kaynak kod ve özgün dokümantasyon [MIT lisanslıdır](LICENSE).
Oyun ikonu, oyun adı, markalar ve diğer üçüncü taraf bileşenleri bu lisansa
dahil değildir; ayrıntılar için [üçüncü taraf bildirimlerine](THIRD_PARTY_NOTICES.md) bak.
MafiaDE resmî bir 2K / Hangar 13 / Epic Games ürünü değildir.

Bu araç çevrimdışı tek oyunculu kullanım içindir. Uyumluluk veya kesintisiz
çalışma garantisi verilmez; kayıtlarını yedekle.

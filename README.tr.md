# Job Application Agent

[English](README.md) · **Yerel prototip · tarayıcı gönderimleri yalnız sentetik**

İlk çalışan akış: CV/profil → ilan değerlendirmesi → kaynaklı cevaplar → gerçek
Chromium ile form doldurma ve CV yükleme → ayrı paylaşım/gönderim onayları →
sunucudan sonuç teyidi. Kurgusal aday ve yerel test sitesi kullanılır; işverene
başvuru gönderilmez.

## Çalıştırma

Windows x64, PowerShell 7, Git ve Node.js/npm gereklidir. Proje klasöründe:

```powershell
./scripts/bootstrap.ps1
./scripts/doctor.ps1
./scripts/verify.ps1
./scripts/run-demo.ps1
```

Başlatıcının gösterdiği özel bağlantıyı açın. Sentetik CV'yi aktarın, profili
doğrulayın, ilan/cevap paketini inceleyin; önce paylaşımı, sonra gönderimi onaylayın.
Ctrl+C iki yerel sunucuyu durdurur. Kurulum eksik .NET SDK'sını kullanıcı hesabına
kurar; yönetici yetkisi veya sistem PATH değişikliği gerekmez. Bağımlılık indirmek
için internet gerekir. Demo API anahtarı ve ücretli model çağrısı kullanmaz.

Profil sürümleri Windows DPAPI ile korunur. Maaş beklentisi ile özel alt sınır ayrı
tutulur. Kişisel proje profesyonel deneyim sayılmaz. Gönderimden sonra bağlantı
koparsa sonuç belirsiz kaydedilir; otomatik tekrar gönderilmez.

“Kendi CV ve ilanım” sekmesinde TXT/PDF/DOCX yükleyebilir, kaynakları seçerek profil
bilgilerinizi doğrulayabilir, ilan metni ve koşullarını inceleyebilir, cevapları
görebilir ve yerel verilerinizi dışa aktarabilir/silebilirsiniz. Bu ekran hiçbir
işveren sitesine bağlanmaz. Tarayıcı denemeleri ayrı sentetik sekmededir.

Genel ürün henüz tamamlanmadı: geniş değerlendirme seti, model üzerinden tam başvuru
akışı ve izinli canlı adaptörler eksik. LinkedIn otomasyonu platform izni olmadan
kapalıdır. Üç salt okunur MCP aracı gerçek alt süreçte test edildi; ayrıca güncel
Codex host'unda yetenek sorgusu başarıyla çalıştırıldı.

[Kurulum](docs/guides/INSTALL.md) · [Sorun giderme](docs/guides/TROUBLESHOOTING.md) ·
[Test kayıtları](docs/VERIFICATION.md) · [Yetenek matrisi](docs/CAPABILITY_MATRIX.md) ·
[Devam kaydı](docs/PROJECT_STATE.md)

Bağımsız kullanıcı ve benimsenme kanıtı yok. Codex
for OSS taslağı hazırlandı; gönderim durumu proje kayıtlarında tutulur ve kabul
garantisi bulunmuyor. Ücretli çağrı ve gerçek başvuru ayrıca kullanıcı onayı gerektirir.

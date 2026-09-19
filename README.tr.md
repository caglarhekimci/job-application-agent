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

Yeni cevapları başvuru/şirket/genel kapsamda, dil ve son kullanma tarihiyle
kaydedebilir veya geri çekebilirsiniz. Model önerisi şema denetimini geçse de
kullanıcı incelemesi olmadan doğrulanmış sayılmaz. Değerlendirme komutu 12 sentetik
profilde 240 soru ve 60 ilan sonucunu çalıştırır; bu sayılar model doğruluğu değildir.

Üç salt okunur MCP aracına ek olarak açıkça etkinleştirilen üç sentetik işlem aracı
vardır. Model taslak hazırlayıp inceleme isteyebilir; gönderim izni yalnız yerel
arayüzde verilir. Gerçek tarayıcı ve MCP birlikte test edildi. CAPTCHA/MFA
görüldüğünde otomasyon durur. Genel form adaptörleri ve izinli canlı hedefler
henüz tamamlanmadı. LinkedIn otomasyonu platform izni olmadan kapalıdır.

[Kurulum](docs/guides/INSTALL.md) · [Sorun giderme](docs/guides/TROUBLESHOOTING.md) ·
[Test kayıtları](docs/VERIFICATION.md) · [Yetenek matrisi](docs/CAPABILITY_MATRIX.md) ·
[Devam kaydı](docs/PROJECT_STATE.md)

Public repo ve ilk ön sürüm yayımlandı. Codex for OSS başvurusu gönderildi ve
OpenAI'nin alındı mesajı görüldü; kabul ve üyelik desteği henüz belli değil.
Bağımsız kullanıcı ve benimsenme kanıtı yok. Ücretli çağrı ve gerçek başvuru
ayrıca kullanıcı onayı gerektirir.

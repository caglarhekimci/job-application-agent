# Job Application Agent

**A free, local application assistant for Windows. Early prototype.**

[English](#english) · [Türkçe](#türkçe) · [Releases](https://github.com/caglarhekimci/job-application-agent/releases) · [Project status](docs/PROJECT_STATE.md)

## English

Review your CV, check a job's requirements and prepare answers from facts you have
confirmed. A separate demo fills a fictional application in a real browser,
uploads a synthetic CV and checks the receipt after your approval.

### What you need

- **Windows x64**, **PowerShell 7**, **Git**, and **Node.js 22 with npm** (the CI-tested version).
- Internet for the first setup. The installer downloads the required .NET SDK,
  dependencies and its own Chromium browser into your user account.
- **No Chrome extension, computer-control plugin, administrator access, API key
  or ChatGPT subscription is needed for the local app and demo.**

### Install and start

Open **PowerShell 7**, then run:

```powershell
git clone https://github.com/caglarhekimci/job-application-agent.git
cd job-application-agent
./scripts/bootstrap.ps1
./scripts/doctor.ps1
./scripts/run-demo.ps1
```

Open the private session link shown in the terminal; keep that link private.
The current interface is in Turkish:

- **“Kendi CV ve ilanım”**: import TXT/PDF/DOCX, confirm profile facts, review a
  pasted job and preview answers. Your data stays in the local workspace.
- **Synthetic demo**: import the fictional CV, review the answers, approve sharing,
  then separately approve submission. Only the local test site receives the form.

Press **Ctrl+C** in the terminal to stop. To run the automated checks:
`./scripts/verify.ps1`.

### Limits and optional Codex integration

This prototype does not submit to real employers. LinkedIn automation is disabled
without platform authorization. Windows is the currently verified platform.
Local workflows make no paid model/API calls. Optional Codex use requires your own
Codex access and uses its normal usage allowance; it is not required to run the app.
The packaged Codex plugin provides read-only inspection tools.

[Installation / reset](docs/guides/INSTALL.md) · [Troubleshooting](docs/guides/TROUBLESHOOTING.md) ·
[Optional Codex setup](docs/guides/CODEX_SETUP.md) · [Privacy](docs/PRIVACY.md) ·
[Test evidence](docs/VERIFICATION.md)

## Türkçe

**Windows'ta çalışan, ücretsiz ve yerel bir başvuru yardımcısıdır. Erken prototiptir.**

CV'nizi inceleyin, ilan koşullarını değerlendirin ve doğruladığınız bilgilerden
cevap hazırlayın. Ayrı demo, gerçek tarayıcıda kurgusal başvuru formunu doldurur,
sentetik CV yükler ve onayınızdan sonra gönderim sonucunu kontrol eder.

### Gerekenler

- **Windows x64**, **PowerShell 7**, **Git** ve **npm içeren Node.js 22** (CI'da test edilen sürüm).
- İlk kurulum için internet. Kurulum betiği gerekli .NET SDK'sını, bağımlılıkları
  ve kendi Chromium tarayıcısını kullanıcı hesabınıza indirir.
- **Yerel uygulama ve demo için Chrome eklentisi, bilgisayar yönetimi eklentisi,
  yönetici yetkisi, API anahtarı veya ChatGPT aboneliği gerekmez.**

### Kurulum ve çalıştırma

**PowerShell 7** açıp çalıştırın:

```powershell
git clone https://github.com/caglarhekimci/job-application-agent.git
cd job-application-agent
./scripts/bootstrap.ps1
./scripts/doctor.ps1
./scripts/run-demo.ps1
```

Terminalde gösterilen özel oturum bağlantısını açın; bu bağlantıyı paylaşmayın.
Mevcut arayüz Türkçedir:

- **“Kendi CV ve ilanım”**: TXT/PDF/DOCX aktarın, profil bilgilerini doğrulayın,
  yapıştırdığınız ilanı inceleyin ve cevapları görün. Veriler yerel çalışma alanında kalır.
- **Sentetik demo**: kurgusal CV'yi aktarın, cevapları inceleyin, önce paylaşımı,
  ardından ayrıca gönderimi onaylayın. Form yalnız yerel test sitesine gider.

Durdurmak için terminalde **Ctrl+C** kullanın. Otomatik kontrolleri çalıştırmak için:
`./scripts/verify.ps1`.

### Sınırlar ve isteğe bağlı Codex bağlantısı

Bu prototip gerçek işverenlere başvuru göndermez. LinkedIn otomasyonu platform izni
olmadan kapalıdır. Şu anda doğrulanan işletim sistemi Windows'tur.
Yerel akışlar ücretli model/API çağrısı yapmaz. İsteğe bağlı Codex kullanımı için
kendi Codex erişiminiz gerekir ve normal kullanım kotanız tüketilir; uygulamayı
çalıştırmak için gerekli değildir. Paketlenmiş Codex eklentisi salt okunur inceleme araçları sunar.

[Kurulum / sıfırlama](docs/guides/INSTALL.md) · [Sorun giderme](docs/guides/TROUBLESHOOTING.md) ·
[İsteğe bağlı Codex kurulumu](docs/guides/CODEX_SETUP.md) · [Gizlilik](docs/PRIVACY.md) ·
[Test kayıtları](docs/VERIFICATION.md) · [Proje durumu](docs/PROJECT_STATE.md)

---

[MIT license / lisans](LICENSE) · [Contributing / katkıda bulunma](CONTRIBUTING.md) ·
[Security / güvenlik](SECURITY.md) · [Dependency notices / bağımlılık bildirimleri](THIRD_PARTY_NOTICES.md)

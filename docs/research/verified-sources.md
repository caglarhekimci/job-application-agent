# W00 resmi kaynak doğrulaması

**Doğrulama tarihi:** 18 Eylül 2026
**Kapsam:** Salt okunur resmî kaynak araştırması. Form gönderilmedi, hesap verisi okunmadı ve haricî sistem değiştirilmedi.

## Doğrulanan bulgular

### Codex for Open Source — program ve form (S01–S03)

- Program sayfası; seçilen bakımcılara Codex'i içeren altı aylık ChatGPT Pro, koşullu Codex Security erişimi ve uygun bakım iş akışları için API kredileri sunulduğunu söylüyor. Başvurular sürekli inceleniyor ve seçilenler e-postayla bilgilendiriliyor. Bu ifadeler kabul, kredi veya Security erişimi garantisi değildir.
- Hedef kitle, aktif/açık kaynak proje bakımcılarıdır. Resmî sayfalar proje kullanımı, ekosistem önemi, aktif bakım ve kişinin primary/core maintainer rolünü değerlendirme sinyalleri olarak sayıyor; yayımlanmış kesin yıldız, indirme veya kabul eşiği vermiyor.
- Güncel formda şu alanlar görünüyor: ad, soyad, ChatGPT hesabı e-postası, public GitHub kullanıcı adı, public repo URL'si, primary/core maintainer seçimi, “Why does this repository qualify?”, Codex Security/API kredisi ilgi seçimleri, OpenAI Organization ID, API kredilerinin kullanım açıklaması ve ek not.
- Üç alanın her biri ayrı ayrı **en fazla 500 karakter**: repo uygunluk açıklaması, API kredisi kullanım açıklaması ve ek not. Ad/soyad, e-posta, GitHub, repo URL'si, rol seçimi veya Organization ID için kaynak sayfada 500 karakter sınırı gösterilmiyor.
- Form işaretlemesi OpenAI Organization ID ile API kredisi kullanım açıklamasını `*` ile gösteriyor. Bu alanların yalnız API kredisi seçildiğinde görünür/zorunlu olup olmadığı, formu etkileşimli olarak değiştirmeden doğrulanamadı; gönderim öncesi canlı durumda yeniden kontrol edilmeli.
- Koşullar geçerli ChatGPT hesabı ile doğru ve eksiksiz kimlik, repo ve bakımcı rolü bilgisi istiyor. OpenAI başvuruyu kabul veya reddedebilir, ek doğrulama isteyebilir; başvuru seçim, finansman veya erişim garantisi vermez. Faydalar aksi yazılı belirtilmedikçe kişisel, sınırlı ve devredilemezdir. Codex Security ve API kredileri ayrı inceleme/koşullara tabi olabilir.

Kaynaklar: [S01 — başvuru formu](https://openai.com/form/codex-for-oss/), [S02 — program sayfası](https://developers.openai.com/community/codex-for-oss), [S03 — program koşulları](https://learn.chatgpt.com/docs/codex-for-oss-terms)

### LinkedIn sınırları (S04–S05)

- LinkedIn Help, LinkedIn sitesinde içerik kazıyan, görünümü değiştiren veya etkinliği otomatikleştiren üçüncü taraf yazılım, bot, tarayıcı eklentisi ve uzantılara izin verilmediğini açıkça belirtiyor.
- Kullanıcı Sözleşmesi; LinkedIn'in ayrıca yazılı izin vermediği kazıma/kopyalama araçlarını, erişim kontrollerini aşmayı ve yetkisiz bot/otomatik yöntemlerle hizmete erişmeyi yasaklıyor.
- Sonuç: Kullanıcının bir başvuruya onay vermesi LinkedIn platform izni oluşturmaz. Yazılı/uygun platform yetkisi doğrulanmadan LinkedIn tarama, form doldurma veya gönderim adaptörü etkinleştirilmemeli. CAPTCHA, erişim kontrolü veya kullanım sınırı aşılmamalı.

Kaynaklar: [S04 — yasaklı yazılım ve uzantılar](https://www.linkedin.com/help/linkedin/answer/a1341387), [S05 — Kullanıcı Sözleşmesi](https://www.linkedin.com/legal/user-agreement)

### Computer use ve browser (S06, S10)

- OpenAI computer-use rehberi, modelin eylem istemesinin kullanıcı izni olmadığını; uygulama ve yürütme ortamının eylem öncesi izin denetimi, risk noktasında durma ve gerektiğinde kullanıcıya devretme kurallarını uygulaması gerektiğini söylüyor.
- Hassas veriyi forma yazmak veri aktarımı sayılıyor ve yazmadan/göndermeden önce onay gerekiyor. Üçüncü tarafa gönderme/başvurma, dosya yükleme ve yeni yazılım/eklenti çalıştırma gibi eylemler için rehberde eylem zamanında onay kuralları var; mevcut açık ve belirli ön onayın yeterli olduğu sınırlı durumlar ayrıca belirtilmiş.
- Browser uzantısı; ChatGPT masaüstü uygulamasında `Settings > Computer Use` üzerinden desteklenen tarayıcı için eklenti kurulmasını, izinlerin gözden geçirilmesini ve sohbet içinde tarayıcının `@` ile seçilmesini anlatıyor. Desteklenen tarayıcılar Chrome, Edge, Brave, Opera ve Vivaldi; erişilebilirlik rollout ve workspace ayarlarına bağlı olabilir.
- ChatGPT varsayılan olarak yeni her web sitesi için izin ister; allowlist/blocklist ayarları vardır. Genel “Allow for all sites” daha yüksek risk taşır. Bu host özelliği, projenin kendi güvenlik motorunun her browser eylemini denetlediği anlamına gelmez.

Kaynaklar: [S06 — computer-use integration](https://developers.openai.com/api/docs/guides/tools-computer-use-integration), [S10 — browser extension](https://learn.chatgpt.com/docs/chrome-extension)

### MCP ve C# SDK (S09 ve resmî SDK belgeleri)

- ChatGPT masaüstü uygulaması, Codex CLI ve IDE uzantısı aynı Codex host üzerinde yapılandırılan MCP sunucularını destekliyor. Desteklenen bağlantılar arasında komutla başlatılan yerel **STDIO** sunucuları ve Streamable HTTP sunucuları bulunuyor.
- Masaüstü/IDE arayüzünde STDIO veya Streamable HTTP seçilebilir. CLI için `codex mcp add ... -- <stdio server-command>` ve `config.toml` için STDIO `command`, isteğe bağlı `args`, `env`, `env_vars` ve `cwd` alanları belgelenmiş.
- MCP'nin resmî SDK listesi C# SDK'yı Tier 1 olarak gösteriyor. Resmî C# rehberi, yerel STDIO tabanlı sunucu için `ModelContextProtocol` paketini başlangıç paketi olarak öneriyor ve `WithStdioServerTransport()` örneğini veriyor. HTTP sunucuları için ayrı `ModelContextProtocol.AspNetCore` paketi var.
- STDIO yerel süreç/tek oturum bağlantısıdır. C# SDK belgesi, üst süreçteki gizli ortam değişkenlerinin çocuk MCP sürecine istemeden aktarılabileceği konusunda uyarıyor; uygulama yalnız gerekli ortam değişkenlerini geçirmeli ve protokol mesajlarını bozacağı için STDOUT'a uygulama logu yazmamalıdır (örnek logları STDERR'e yönlendiriyor).

Kaynaklar: [S09 — ChatGPT/Codex MCP](https://learn.chatgpt.com/docs/extend/mcp?surface=cli), [MCP resmî SDK listesi](https://modelcontextprotocol.io/docs/sdk), [C# SDK başlangıç rehberi](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md), [C# SDK transport rehberi](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md)

### .NET destek politikası

- Microsoft'un 8 Eylül 2026 güncellemeli destek tablosu **.NET 10'u aktif LTS** olarak listeliyor; tabloda güncel patch `10.0.12`, destek sonu `14 Kasım 2028`.
- Bu kaynak hedef framework seçimini destekler; bu araştırma yerel makinede kurulu tam SDK sürümünü doğrulamamıştır. Tam SDK kurulumu ve `dotnet --info` kanıtı ana W00 yürütücüsünün ayrı ortam işidir; kurulmuş gibi işaretlenmemeli.

Kaynak: [Microsoft — .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)

## Engelli veya doğrulanmamış gerçekler

- Projenin public GitHub deposu, release'i, kullanıcı benimsenmesi, indirme/yıldız sayısı, gerçek bakım faaliyeti ve primary/core maintainer rolü bu araştırmada doğrulanmadı.
- ChatGPT hesap e-postası ve OpenAI Organization ID bilinmiyor; tahmin edilmemeli ve public belgeye yazılmamalı.
- Canlı formdaki Organization ID/API kredisi alanlarının koşullu görünürlüğü ve zorunluluğu, seçim yapmadan doğrulanmadı.
- Bu makinedeki exact .NET SDK sürümü, browser eklentisinin kurulu/etkin oluşu ve MCP host bağlantısı bu kaynak taramasının kapsamı dışındaydı.
- LinkedIn'den bu proje için ayrı yazılı otomasyon izni veya resmî başvuru API yetkisi doğrulanmadı.
- Başvuru yapılmadı; uygunluk, seçim, API kredisi, Codex Security erişimi veya kabul iddiası yoktur.

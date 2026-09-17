# Job Application Agent — Ana Tasarım, Uygulama ve Codex for OSS Başvuru Planı

**Belge sürümü:** 1.0  
**Araştırma ve doğrulama tarihi:** 18 Eylül 2026  
**Proje sahibi / hedef GitHub hesabı:** `caglarhekimci`  
**Önerilen depo adı:** `job-application-agent`  
**Hedef depo:** `caglarhekimci/job-application-agent` — öneridir; bu belge hazırlanırken oluşturulmadı.  
**Belge durumu:** Uygulanacak tasarım ve iş planı. Ürünün geliştirilmiş, testlerinin geçmiş, LinkedIn izninin alınmış veya program başvurusunun yapılmış olduğu anlamına gelmez.

> **Uygulayıcı yapay zekâ için:** Bu belge hem ürün şartnamesini hem aşamalı uygulama planını içerir. Önce tamamını oku. Ortamda mevcutsa ilgili geliştirme becerilerini kullan; uygulamayı test güdümlü ve doğrulanabilir iş paketleriyle yürüt. Tasarımın kendisini yeniden üretmekle yetinme. Haricî izinleri, gerçek kullanıcıları ve program kabulünü kod yazarak elde edilmiş sayma.

**Amaç:** Kullanıcının CV'si ve doğrulanmış tercihleriyle uygun işleri değerlendiren, başvuru sorularını yanıtlayan, izinli tarayıcı akışlarında form doldurup gerekli kullanıcı onayından sonra başvuruyu gönderen; açık kaynak geliştiricilerinin yeniden kullanabileceği bir ajan geliştirmek. Bu ürünü gerçekten kullanmak, bakımını yapmak ve oluşan kanıtlarla Codex for Open Source programına başvurmak.

**Mimari:** Önce yerel ve tek kullanıcılı uygulama. .NET uygulama çekirdeği; kalıcı aday profili; kurallı cevap ve onay motoru; denetimli tarayıcı çalıştırıcısı; yerel MCP bağlantısı; sentetik başvuru sitesi ve değerlendirme takımı. Bulut hizmeti, mağaza yayını ve tam masaüstü kontrolü ayrı, koşullu genişlemelerdir.

**Teknoloji:** Varsayılan .NET 10 LTS, ASP.NET Core, C#, EF Core/SQLite, resmî C# MCP SDK ve Playwright for .NET. Küçük yerel arayüz React + TypeScript; bağımlılıklar geliştirme başında resmî belgelerle doğrulanıp sabitlenecek. Model ve sağlayıcı kimlikleri yapılandırılabilir olacak. [S13][s13] [S14][s14] [S15][s15]

**Şartname:** Bölüm 0–17. **Uygulama:** Bölüm 18–21. **Gerçek kullanım ve program başvurusu:** Bölüm 22–26. **Teslim ve kaynaklar:** Bölüm 27–29.

---

## 0. Belgeyi nasıl kullanacağız?

Bu dosyayı proje köküne `JOB_APPLICATION_AGENT_MASTER_PLAN_TR.md` adıyla koy. Yanındaki `CODEX_EXECUTION_PROMPT_TR.md` dosyasını yürütme talimatı olarak kullan. Kullanıcı açısından hedef yalnızca bir rapor veya CV editörü değil, çalışan başvuru iş akışıdır.

Belgedeki ifadeler üç sınıfa ayrılır:

- **Doğrulanmış dış bilgi:** Kaynak numarasıyla işaretlenir. Uygulamadan ve başvurudan önce güncelliği tekrar kontrol edilir.
- **Tasarım kararı / iç hedef:** Bu proje için önerdiğimiz seçimdir. OpenAI'nin resmî değerlendirme ölçütü veya elde edilmiş sonuç değildir.
- **Haricî bağımlılık:** İnsan onayı, platform izni, gerçek pilot, mağaza incelemesi veya program kararı gerekir. Bunlar yalnızca kodla tamamlanamaz.

### 0.1 Değişmez kurallar

1. Pro desteği kazanılacağı garanti edilmez; çalışmanın hedefi dürüst ve güçlü bir adaylık oluşturmaktır.
2. CV'de olmayan iş deneyimi, unvan, dil seviyesi veya çalışma izni üretilmez.
3. LinkedIn'de otomasyon izni ile kullanıcının kendi verisini paylaşma onayı farklıdır.
4. Başvuru gönderimi ve kişisel veri aktarımı, ilgili yetki ve onay kontrollerinden geçer.
5. Gerçek CV, maaş bilgileri, adres, telefon, oturum çerezleri ve API anahtarları public repoya girmez.
6. Sentetik demo, gerçek platform entegrasyonu veya bağımsız kullanıcı benimsemesi diye sunulmaz.
7. Sahte yıldız, indirme, PR, yorum, kullanıcı, referans veya bakım geçmişi oluşturulmaz.
8. Sırf destek programı için yapay model eğitimi, masaüstü kontrolü veya bulut altyapısı eklenmez.
9. Mevcut işverenin/önceki işverenlerin kodu ve özel verisi, açık kaynak projeye taşınmaz.
10. Çalıştırılmayan test, gönderilmeyen form, yayınlanmayan release veya alınmayan izin tamamlanmış sayılmaz.

### 0.2 Oturumlar arasında devamlılık

İlk uygulama oturumunda `docs/PROJECT_STATE.md`, `docs/DECISIONS.md` ve `docs/VERIFICATION.md` oluştur. Her anlamlı iş paketinin sonunda güncelle. Bir sonraki yapay zekâ önce bu dosyaları, sonra ilgili iş paketini okumalı. Sohbet hafızasına veya aynı modelin her zaman aynı bağlamı hatırlayacağına güvenme.

Durumlar: `Planned`, `InProgress`, `Implemented`, `VerifiedLocal`, `VerifiedCI`, `VerifiedLiveAuthorized`, `BlockedExternal`.

`Implemented`, çalışan test kanıtı değildir. `VerifiedLocal`, gerçek LinkedIn entegrasyonu değildir. `BlockedExternal`, bütün projenin tamamlanmadığını dürüstçe gösteren meşru bir durumdur.

## 1. Giriş: Asıl hedef ve başarı tanımı

Projenin iki insanî hedefi vardır: Çağlar'ın iş başvurularındaki tekrar işlerini azaltmak ve sürdürülebilir bir açık kaynak projenin bakımcısı olarak altı aylık ChatGPT Pro desteğine aday olmak. Teknik hedef, bu ikisini gerçek bir fayda üzerinden birleştirmektir.

### 1.1 Kullanıcıya verilecek temel deneyim

> “CV'mi paylaşayım. Hangi şehirlerde ve nasıl çalışabileceğimi, maaş beklentimi ve deneyimimi bir kez netleştireyim. Ajan uygun işleri bulup değerlendirsin; soruları benim doğru bilgilerimle doldursun; yalnızca yeni bilgi veya gerekli onay gerektiğinde dursun; gönderdiği başvurunun sonucunu takip etsin.”

LinkedIn, kullanıcının öncelikli iş keşif kaynağıdır. Buna rağmen ilk çalışan çekirdeğin tek bir platforma bağımlı olması istenmez. Platform yetkisi sağlanmadan tam LinkedIn otomasyonunun tamamlandığı iddia edilmeyecek; bu özellik ayrı bir canlı entegrasyon kapısı olarak tutulacaktır.

### 1.2 Üç ayrı başarı düzeyi

| Düzey | Başarının kanıtı | Kontrol kimde? |
|---|---|---|
| Mühendislik | Temiz kurulum, çalışan profil hafızası, gerçek tarayıcıdaki sentetik E2E, güvenlik testleri, doğrulanmış sonuç kaydı | Büyük ölçüde geliştiricide |
| Açık kaynak faydası | Başka insanların kurabilmesi, gerçek geri bildirim, tekrar kullanım, anlamlı bakım ve sürümler | Geliştirici + topluluk |
| Program desteği | OpenAI'nin olumlu değerlendirmesi ve faydanın tanımlanması | OpenAI |

İlk düzeye ulaşıldığında üçüncü düzey otomatik oluşmaz. Değerlendirmede güçlü olmanın yolu, etkisi kanıtlanmamış özellikleri çoğaltmak değil, başkalarının gerçekten yararlanabildiği ve sürdürülen bir ürün oluşturmaktır.

### 1.3 Açık kaynak değer önerisi

Genel bir “AI wrapper” yerine şu parçalar bağımsız olarak da faydalı olmalı:

- **Aday hafızası:** Kaynağı, güncelliği, kapsamı ve paylaşım izni bilinen kariyer bilgileri.
- **Cevap doğrulama motoru:** Maaş dönemi, net/brüt, profesyonel/kişisel deneyim gibi ayrımları koruyan kurallar.
- **Onaylı eylem motoru:** Tam olarak hangi verinin hangi işe gönderildiğini izleyen durum makinesi.
- **Başvuru formu test ortamı:** Kişisel verisiz, yeniden üretilebilir çok adımlı form ve saldırı senaryoları.

İlk sürümde bunlar tek repoda modüler tutulur. Ayrı NuGet paketlerine bölmek ancak gerçek yeniden kullanım ihtiyacı oluştuğunda yapılır; başvuru formunda daha büyük görünmek için gereksiz repo/paket çoğaltılmaz.

## 2. Altı aylık Pro programı: Doğrulanmış çerçeve

### 2.1 Programın özeti

OpenAI'nin Codex for Open Source programı, seçilen bakımcılara Codex'i içeren altı aylık ChatGPT Pro sunuyor; Codex Security ve API kredileri ayrı/koşullu değerlendirmeye tabi. Başvurular dönemsel bir son tarih yerine devamlı inceleniyor, seçilen kişilere e-posta gönderiliyor. [S01][s01]

Program açıklaması; aktif bakım, anlamlı kullanım, benimsenme veya yazılım ekosistemindeki önem gibi göstergelere odaklanıyor. Her projeye uygulanacak yayımlanmış kesin bir yıldız ya da indirme barajı doğrulanmadı. Bu, yeni bir reponun kabul edileceği anlamına gelmez. Geleneksel ölçülere uymayan projeler önemlerini açıklayabilir. [S02][s02]

Seçim OpenAI'nin takdirindedir; doğru kimlik ve bakımcı bilgisi gerekir. Koşullar değişebilir; yanlış beyan, mükerrer kimliklerle fayda arama veya faydayı devretme gibi davranışlar yaptırıma konu olabilir. [S03][s03]

### 2.2 Bizim kontrol edebileceğimiz hazırlıklar

Aşağıdakiler **iç hazırlık ölçütleridir**, resmî kabul formülü değildir:

| Hazırlık | Üreteceğimiz gerçek kanıt |
|---|---|
| Çalışan açık kaynak ürün | Lisanslı kaynak kod, temiz ortam kurulum testi, etiketli release |
| Bakım sorumluluğu | Gerçek issue değerlendirmeleri, fix PR'ları, sürüm notları |
| Kullanıcı yararı | İzinli ve anonimleştirilmiş pilot geri bildirimi, yeniden üretilebilir zaman/kalite ölçümü |
| Ekosistem yararı | Tekrar kullanılabilir sözleşmeler, örnek MCP kurulumu, sentetik test paketi |
| Güvenilirlik | Güvenlik sınırları, doğru özellik matrisi, başarısızlıkların da raporlandığı test sonuçları |
| Destek ihtiyacı | Codex ile yapılacak somut bakım, test, migration ve inceleme işleri |

### 2.3 Yapılması şart olmayan işler

Doğrulanan başvuru ölçütlerinde “ChatGPT mağazasında yayınlanmış olmalı”, “LinkedIn hesabını bağlamalı”, “tam bilgisayar kontrolü kullanmalı”, “kendi temel modelini eğitmeli” veya “ürünü yalnızca Codex ile yazmalı” şeklinde bir ön şart bulunmuyor. Bu işleri yalnızca ürün faydası sağlıyorlarsa yapacağız. [S02][s02]

Proje yeni olduğu için destek alamayabilir. Bu olasılığı azaltmak için gerçek kullanım ve bakım oluşturacağız; bir kabul yüzdesi, garantili bekleme süresi veya kesin yıldız hedefi uydurmayacağız.

## 3. Stratejik karar: Ne geliştireceğiz, neyi erteleyeceğiz?

### 3.1 Seçilen başlangıç

**Yerel çalışan ajan + C# MCP sunucusu + denetimli tarayıcı + sentetik test sitesi.**

Bu seçim, kişisel kullanım ile başkalarının kaynak kodu indirip deneyebilmesini aynı çekirdekte toplar. Kullanıcının bilgisayarında çalışan süreç ile ChatGPT/Codex arayüzü ayrı parçalar olarak kalır. Abonelik, model erişimi ve tarayıcı özelliği geliştirme ortamında gerçekten doğrulanır; bütün hesaplarda var sayılmaz.

### 3.2 Alternatiflerin yeri

| Alternatif | Ne zaman kullanılır? | Neden ilk şart değil? |
|---|---|---|
| Yalnız skill/prompt | Kullanım yönergesi ve hafif prototip | Tek başına kalıcı/verifiye başvuru durumu ve deterministik güvenlik sağlamaz |
| Yerel MCP + yönetilen browser | İlk ana ürün | Tekrarlanabilir, test edilebilir, modelden bağımsız çekirdek sağlar |
| ChatGPT/Codex'in kendi Chrome özelliği | Desteklenen hesapta kişisel yardımcı akış | Dış host'un eylemleri bizim çekirdeğin denetiminden geçmeyebilir |
| Kendi Chrome uzantımız | Mevcut adaptörle çözülemeyen, kanıtlanmış ihtiyaca göre | Tarayıcı izinleri, yayın ve güvenlik yüzeyini artırır |
| Tam masaüstü kontrolü | Web dışı bir adım gerçekten gerekirse | Sırf Pro desteği için gereksiz; odak ve yanlış pencere riski taşır |
| Barındırılan SaaS / public mağaza | Yerel ürün ve izinler olgunlaştıktan sonra | Çok kiracılık, veri koruma, yetkilendirme ve işletim yükü getirir |

### 3.3 Bağımlılık sırası

Önce ürün çekirdeği ve güvenilir test → ardından gerçek kullanım → ardından gerekli izinleri olan canlı entegrasyon → ardından OSS başvuru dosyası. Mağaza başvurusu ayrı yürür; yerel ürün ve grant başvurusunu gereksiz yere bekletmez.

LinkedIn tarafında yetki bulunamazsa iki gerçek açık kalır: yeniden kullanılabilir ajan geliştirilebilir; fakat istenen tam LinkedIn otomasyonu teslim edilmiş değildir. Bu fark `docs/CAPABILITY_MATRIX.md` ve release notlarında görünür tutulur.

## 4. Ürün şartları ve kapsam

### 4.1 Temel kullanıcı yolculukları

**Yolculuk A — İlk kurulum:** Kullanıcı yerel uygulamayı açar, CV'sini seçer; çıkarılan deneyim, teknoloji ve tarihleri kontrol eder. İletişim bilgilerini, iş tercihlerini, maaş paylaşım kurallarını ve cevabı bilinmeyen alanları görür. Doğrulanan profil kaydedilir.

**Yolculuk B — Başvuru:** Kullanıcı ilan metni/izinli kaynak/iş listesi verir. Ajan uygunluk gerekçelerini çıkarır, yinelenen başvuruyu kontrol eder, gerekli form cevaplarını hazırlar, izinli hedefte ilerler. Bilinmeyen bir soruda durur; cevabın sonraki işler için de kullanılacağını ayrıca netleştirir. Veri paylaşımına ve son gönderime ilişkin gerekli onaylarla işlemi tamamlar.

**Yolculuk C — Tercih değişikliği:** Kullanıcı “bundan sonra maaş beklentim farklı” der. Sistem eski değeri sessizce silmek yerine yeni profil sürümü oluşturur. Önceki cevap paketlerinin hangi sürüme dayandığı izlenir; gönderilmemiş eski paketler yeniden değerlendirilir.

**Yolculuk D — İşlem yarıda kalır:** Tarayıcı kapanır veya ağ hatası olur. Yeniden açıldığında kayıtlı duruma dönülür. Gönderimin yapılıp yapılmadığı belirsizse tekrar başvuru gönderilmez; doğrulama veya kullanıcı incelemesi istenir.

### 4.2 Gereksinimler

| Kimlik | Gereksinim | Öncelik | Kabul kanıtı |
|---|---|---|---|
| FR-01 | CV'yi yerel içeri aktar, kaynak kanıtları çıkar, kullanıcıya doğrulat | P0 | Sentetik PDF/DOCX/metin testleri ve doğrulama ekranı |
| FR-02 | Tercih ve cevapları kalıcı, sürümlü sakla | P0 | Uygulama yeniden başlatılınca doğru sürüm okunur |
| FR-03 | Maaş tutarı, para birimi, dönem, net/brüt ve paylaşım sınırlarını ayır | P0 | Maaş sınır durumları test takımı |
| FR-04 | İlanı sert koşullar, tercihler ve bilinmeyenler açısından değerlendir | P0 | Açıklanabilir uygunluk çıktısı |
| FR-05 | Aynı işe tekrar başvurmayı engelle | P0 | Tekrarlı tıklama/yeniden başlatma testleri |
| FR-06 | Türkçe/İngilizce soruları doğru dilde, kanıta dayalı yanıtla | P0 | İki dilde cevap değerlendirmesi |
| FR-07 | Tarayıcıda çok adımlı formu gerçekten doldur | P0 | Canlı yerel test sitesinde ekran ve sunucu kaydı |
| FR-08 | CV dosyasını doğru işe ve doğru sürümle yükle | P0 | Dosya hash'i ve form yükleme kontrolü |
| FR-09 | Yetki/onay yokken bilgi aktarma ve gönderimi durdur | P0 | Negatif testlerde dış etki sıfır |
| FR-10 | Onaydan sonra gönder ve sonucunu ayırt et | P0 | Receipt / belirsiz sonuç ayrımı |
| FR-11 | MCP ile model host'una dar araçlar sun | P0 | Protokol testi + gerçek host smoke testi |
| FR-12 | Kişisel verisiz demo ve offline test modu | P0 | API anahtarsız temiz kurulum |
| FR-13 | İzinli canlı iş kaynağı/adaptörü | P1, haricî koşullu | Belgelenmiş yetki + kontrollü canlı test |
| FR-14 | LinkedIn'de otomatik keşif ve başvuru | P1, haricî koşullu | Platform yetkisi + test; yoksa açıkça engelli |
| FR-15 | CV/ön yazı uyarlaması ve değişiklik karşılaştırması | P1 | Yeni iddia eklenmeyen, kullanıcı onaylı belge |
| FR-16 | Model/sağlayıcı ve maliyet sınırı seçimi | P1 | Bütçe/çağrı limiti testi |
| FR-17 | ChatGPT mağaza paketi | P2, ayrı hedef | Güncel yayın şartlarıyla inceleme paketi |
| FR-18 | Tam bilgisayar kontrolü | P2, yalnız gerekçeyle | Tarayıcıyla çözülemeyen örnek + güvenli ayrı test |

### 4.3 İlk sürümde yapılmayacaklar

Gizli toplu başvuru, otomatik işe alımcı mesaj spam'i, CAPTCHA çözme servisi, oturum çerezi ihracı, ağ engeli aşma, sahte deneyim üretme, başkasının teknik sınavını gizlice çözme, vergi hesabıyla otomatik net/brüt dönüşüm, ödeme alma ve adayları işverene otomatik eleme ürünü kapsamda değildir.

Başvuruda sıradan kariyer sorularına cevap hazırlamak ile işverenin adayın bizzat yapmasını istediği değerlendirmeyi onun yerine gizlice yapmak ayrıdır. İkinci durumda ajan başvuru otomasyonu rolünü aşmaz; kullanıcıya devreder.

## 5. Aday profili: Kalıcı hafıza, kaynak ve izin

### 5.1 ChatGPT hafızası yerine ürünün doğrulanmış kayıtları

Kullanıcı “bunu hatırla” dediğinde ürün içinde sürümlü bir kayıt oluşmalıdır. Başka bir sohbetin tamamına veya ChatGPT'nin tüm hafızasına erişim varsayılmaz. Modelin önerdiği profil değişikliği, doğrulanmış veri ile aynı statüde değildir.

MCP aracı `approved: true` parametresi kabul ederek modelin kendi onayını üretmesine izin vermemeli. Model yalnızca değişiklik teklif eder; kullanıcı güvenilir yerel arayüzde kabul eder veya açık, yetkilendirilmiş host akışı üzerinden doğrular. Denetim kaydı onay olayının kaynağını taşır.

### 5.2 Veri sözleşmeleri

| Varlık | Temel alanlar | Değişmez kural |
|---|---|---|
| `CandidateProfile` | `Id`, `Version`, `Locale`, `VerifiedAt`, `Facts`, `Preferences` | Son doğrulanmış sürüm açık olmalı |
| `EvidenceFact` | `Id`, `Kind`, `Value`, `SourceDocumentId`, `SourceSpan`, `Confidence`, `VerificationStatus`, `ValidFrom`, `ValidUntil` | Kaynaksız çıkarım doğrulanmış deneyim olamaz |
| `ExperiencePeriod` | `Start`, `End`, `Role`, `ExperienceKind`, `EmploymentFraction`, `SkillEvidenceIds` | Profesyonel / staj / part-time / kişisel proje ayrı |
| `SalaryPreference` | `Target`, `PrivateMinimum`, `Currency`, `Period`, `TaxBasis`, `Negotiable`, `DisclosurePolicy`, `Scope` | Filtreleme alt sınırı otomatik paylaşılmaz |
| `AnswerMemory` | `SemanticKey`, `Answer`, `Scope`, `Language`, `EvidenceIds`, `UpdatedAt`, `ExpiresAt` | Şirkete özgü cevap globalleşmez |
| `ConsentPolicy` | `RecipientScope`, `DataCategories`, `Purpose`, `ExpiresAt`, `RevokedAt` | Yetki, belirli amaç ve kapsamla sınırlı |
| `ProfilePatch` | `BaseVersion`, `ProposedChanges`, `ReviewStatus` | Eski sürüme yazma çakışması görünür hata |

Kimlikler GUID/ULID gibi opak değerler olabilir; modelin kişisel veriyi taşıyan dosya yollarını tahmin etmesine gerek yoktur. Para `decimal` ile saklanır, `float` kullanılmaz. Tarihler UTC saklanır; kullanıcı gösteriminde tercih edilen saat dilimi uygulanır. Salt tarih alanlarında gereksiz saat dilimi dönüşümü yapılmaz.

### 5.3 Maaş örneği — tamamen sentetik

```json
{
  "schemaVersion": 1,
  "profileVersion": 3,
  "salary": {
    "target": { "amount": 100000, "currency": "TRY", "period": "Month", "taxBasis": "Net" },
    "privateMinimum": { "amount": 85000, "currency": "TRY", "period": "Month", "taxBasis": "Net" },
    "disclosePrivateMinimum": false,
    "negotiable": true,
    "scope": { "type": "Default" },
    "approvedConversions": []
  }
}
```

Bu tutarlar Çağlar'ın gerçek maaşı veya beklentisi değildir. Gerçek kurulumda kullanıcıdan alınır. Son doğrulama zamanı kullanıcının gerçek kaydından üretilir; örnek veriye gerçekmiş gibi geçmiş tarih yazılmaz.

### 5.4 Gerekli davranışlar

- Aynı para birimi, aynı dönem ve aynı net/brüt sorusunda onaylı beklenti tekrar sorulmadan kullanılabilir.
- “Mevcut maaş” sorusu “beklenti” ile cevaplanmaz.
- Aylık net değer yıllık brüt alana yazılmaz. Onaylı bir dönüşüm yoksa `NeedsInput` döner.
- Para birimi belirsizse formun bağlamı incelenir; kesinleşmiyorsa kullanıcıya sorulur.
- Aralık alanı ile tek tutar alanı ayırt edilir; özel alt sınır açıklanmaz.
- “Bu şirket için taşınırım” cevabı `Company` kapsamıyla saklanır.
- İşe başlangıç/ihbar gibi zamanla değişen cevaplar için geçerlilik tarihi tutulur.
- Aynı tarihlere denk gelen iki profesyonel işin takvim süresi toplam deneyimi otomatik ikiye katlamaz; kural ve kanıt kullanıcıya gösterilir.
- “Java kişisel projesi” profesyonel Java yılına çevrilmez.
- Yaşanılan şehir ile taşınılabilecek şehir ayrı kalır.
- Kullanıcı kaydı silebilir, dışa aktarabilir, izni geri çekebilir; geri çekme daha önce işverene gönderilmiş veriyi otomatik geri alamaz.

## 6. İş keşfi, uygunluk ve kaynak yetkisi

### 6.1 Kaynak tipleri

`UserProvidedPosting`: Kullanıcının bilerek paylaştığı ilan metni ve isteğe bağlı bağlantı. Bağlantıyı kaydetmek otomatik ziyaret izni sayılmaz.

`AuthorizedFeed`: Kullanım şartı/lisansı bu akışa uygun kaynak. Hangi alanları, hangi süre ve amaçla saklayabileceğimiz belgelenir.

`AuthorizedCareerSite`: Başvuru süreci için gerekli yetkilerin bulunduğu işveren/ATS sayfası. GET erişimi, POST gönderim yetkisi yerine geçmez; örneğin Greenhouse Job Board API, okuma ile başvuru gönderimi için farklı yetkilendirme gerektirir. [S16][s16]

`LinkedInRestricted`: Platform yetkisi doğrulanmamış LinkedIn otomasyonu. Varsayılan `PermissionRequired`.

### 6.2 LinkedIn için dürüst ürün sınırı

LinkedIn'in yayımladığı kurallar izinsiz araçlarla faaliyet otomasyonunu sınırlandırıyor. Kullanıcının şifresiz mevcut Chrome oturumunu kullanmak, bu izni kendiliğinden sağlamaz. Hesap kısıtlama riski vardır. [S04][s04]

Bu nedenle:

- `LinkedIn` adaptörü yetki kaydı olmadan `Search`, `ReadPage`, `Autofill`, `Submit` işlemlerini açmaz.
- “Risk bana ait” kutusu platform izni yerine kullanılamaz.
- Yetki gerekiyorsa resmî iş ortaklığı/geliştirici yolu araştırılır; mümkün olmayan erişim “API bulundu” diye uydurulmaz.
- Kullanıcı LinkedIn'de bulduğu ilanı elle paylaşabilir. Sistem bunu analiz edebilir; bu akış “otomatik LinkedIn tarama” değildir.
- İlanın ayrı kariyer sitesine yönlenmesi durumunda yeni hedefin yetkileri ayrıca değerlendirilir.
- Kaynak içeriğini yeniden dağıtmak, marka kullanımı ve otomatik erişim konuları ayrı kontrol edilir. LinkedIn veya OpenAI ile resmî ortaklık varmış gibi marka sunumu yapılmaz. [S05][s05]

### 6.3 Eşleştirme modeli

Sıra: veri normalleştirme → kesin şart kontrolü → tercihler → kanıt tabanlı açıklama → adayın kararına sunma.

Sonuç türleri: `Eligible`, `ReviewNeeded`, `HardRequirementMismatch`, `InsufficientInformation`.

Her gereksinim için `RequirementText`, `RequirementType`, `CandidateEvidenceIds`, `Assessment`, `Reason` döndür. Deneyim, konum/uzaktan çalışma, dil, çalışma izni ve sözleşme tipi ayrı değerlendirilir. “Tercihen 5 yıl” ile “en az 5 yıl zorunlu” aynı değildir.

İstenirse 0–100 iç uyum puanı gösterilebilir; bunun işe alınma olasılığı veya evrensel ATS puanı olmadığı açık yazılır. Eksik bilgi, olumlu eşleşme sayılmaz. Kullanıcı gereksinim uyuşmazlığı olan işe yine başvurmak isterse gerçek deneyim aynen korunur; zorunlu evet/hayır sorularına yalan söylenmez.

### 6.4 İlan kimliği ve tekrar kontrolü

Öncelik kaynak ilan kimliği; yoksa normalleştirilmiş kaynak + şirket + dış başvuru kimliği. Sadece iş unvanına göre birleştirme yapılmaz. Kaynak URL'nin takip parametreleri temizlenir; başvuruyu değiştiren anlamlı parametreler korunur.

`JobPosting` kaydı en az `Source`, `ExternalId`, `CanonicalUrl`, `Employer`, `Title`, `TextHash`, `FirstSeenAt`, `LastCheckedAt`, `PermissionStatus` taşır. İlan kapanmış veya metni önemli ölçüde değişmişse eski başvuru planı yeniden gözden geçirilir.

## 7. Soru anlama ve cevap motoru

### 7.1 İş akışı

1. Form etiketini, seçenekleri, zorunluluk durumunu ve yakın bağlamı oku.
2. Soruyu anlam anahtarına eşleştir: örneğin `salary.expected.monthly.net`, `salary.current`, `experience.professional.csharp.years`.
3. Onaylı aday bilgisi ve aynı kapsamdaki cevap hafızasını ara.
4. Deterministik dönüşüm mümkünse uygula; gerekçesini kaydet.
5. Açık uçlu cevap gerekiyorsa yalnız ilgili kanıtları modele ver.
6. Üretilen cevabı şema, kanıt ve paylaşım politikasıyla doğrula.
7. Sonuç `Resolved`, `NeedsInput`, `RequiresReview`, `ManualOnly` veya `Blocked` olur.

Model güven puanı, tek başına cevap verme yetkisi değildir. `Resolved` bir cevabın hangi kanıta dayandığı ve hangi alıcıyla paylaşılabildiği belirli olmalıdır.

### 7.2 Dil ve uzunluk

Türkçe soru Türkçe; İngilizce soru İngilizce cevaplanır. Başvuru alanının karakter sınırı uygulanır. Varsayılan açık uçlu kısa cevap 2–3 cümledir; ilan daha uzun anlatım istiyorsa alan sınırı içinde genişletilir.

“Uzman” unvanı otomatik “Senior” yapılmaz. Kişisel projeler işveren deneyimi gibi anlatılmaz. Çalışanın şirket verilerine ait gizli metrikler kullanıcının açık ve geçerli paylaşım yetkisi olmadan CV'ye eklenmez.

### 7.3 Karar tablosu

| Soru | Kaynak/koşul | Davranış |
|---|---|---|
| İletişim e-postası | Onaylı, bu alıcıya paylaşılabilir | Alanı doldur |
| Maaş beklentisi | Birim ve kapsam tam eşleşiyor | Kayıtlı beklentiyi kullan |
| Yıllık brüt beklenti | Yalnız aylık net kayıt var | Sor; sayı uydurma |
| “5+ yıl tecrübeniz var mı?” | Doğrulanmış süre daha az | Gerçeğe uygun cevap; eksikliği gizleme |
| “Neden bu rol?” | İlan + adayın ilgili deneyimi | Kısa taslak; kanıt dışı iddia kontrolü |
| Çalışma izni | Bilinmiyor | Kullanıcıdan bilgi iste |
| Mevcut maaş | Paylaşım tercihi yok | Kullanıcıya sor; beklentiyi kopyalama |
| Sağlık, kimlik numarası, hassas demografi | Özel veri alanı | Modelle toplama/çıkarım yapma; kullanıcıya devret |
| İşveren şartlarını kabul kutusu | Kullanıcı okumadı/onaylamadı | Otomatik kabul etme |
| Teknik değerlendirme/kişisel beyan | Adayın bizzat yapması isteniyor | Manuel tamamlamaya yönlendir |

### 7.4 Hatırlama kuralları

Yeni cevap alındığında “yalnız bu başvuru”, “bu şirket”, “bu rol grubu” veya “varsayılan” kapsamı seçilir. Tek seferlik açıklama gizlice profil gerçeğine dönüştürülmez. Kullanıcı yanlış cevabı düzelttiğinde ilgili türetilmiş cevaplar geçersizleşir; aynı hatayı tekrar üretmemek için yalnızca anonim kural/test düzeltmesi public projeye eklenebilir.

## 8. Başvurunun durum makinesi ve gerçek gönderim

### 8.1 Durumlar

```text
Discovered
  -> Evaluated
  -> Selected
  -> Drafting
  -> NeedsInput | ManualStep | BlockedPermission
  -> ReadyForDataSharing
  -> Filling
  -> ReadyForReview
  -> AwaitingSubmissionApproval
  -> Submitting
  -> SubmittedVerified | SubmittedUnverified | FailedBeforeSubmission
```

Her aşamadan kullanıcı iptali mümkündür: `Cancelled`. Başvuru gönderildikten sonra iptal, gönderilmiş başvuruyu otomatik geri çekmez. Kullanıcı geçmişte elle başvurduğunu bildirirse `ReportedByUser` ayrı tutulur.

`SubmissionAttempt` kaydı; iş kimliği, profil sürümü, CV hash'i, cevap paketi hash'i, onay kimliği, başlangıç zamanı ve sonuç kanıtı taşır. Ham kişisel veri audit log'a kopyalanmaz.

### 8.2 Gönderim yetkisi

OpenAI'nin computer-use rehberi, üçüncü taraflara kullanıcı adına gönderme gibi eylemler için işlem anında onay ister; açıkça tanımlanmış yakın işlemler birlikte değerlendirilebilir, belirsiz gelecekteki eylemler için sınırsız onay yerine geçmez. Bir forma bilgi yazılması da veri aktarımı olabilir. [S06][s06]

Bizim uygulama tasarımımız:

- Form doldurmadan önce alıcı, iş ve paylaşılacak veri kategorileri için geçerli dar yetki aranır.
- Son gönderimden önce şirket, ilan, önemli cevaplar ve CV sürümü görünür olur.
- Kullanıcı onayı, belirli bir pakete bağlanır. Kullanıcı her alanı yeniden yazmaz; hazır paketi kontrol eder.
- Aynı ekranda birden çok hazır başvuru onaylanacaksa her birinin alıcısı ve içeriği ayrı görünür, her biri için bağımsız kayıt üretilir.
- Yeni soru, değişmiş şirket/ilan/URL, değişmiş CV veya cevaplar onayı geçersizleştirir.
- Onay almadan gerçek işverene “test başvurusu” gönderilmez.

### 8.3 Onay jetonu

`ApprovalReceipt`: `Id`, `UserSessionId`, `ApplicationId`, `PayloadHash`, `RecipientOrigin`, `ProfileVersion`, `ResumeHash`, `ApprovedAt`, `ExpiresAt`, `UsedAt`.

Varsayılan kısa geçerlilik süresi 10 dakikadır; bu bir **iç tasarım kararıdır**. Güvenilir kullanıcı etkileşiminde oluşturulur, tek kullanımlıdır. Modelin çağırabileceği `approve_everything` veya `mint_approval` aracı olmaz. Beklemede içerik değişirse yeniden onay gerekir.

Tam paket özeti, hassas alanlar dahil değişikliği yakalayacak şekilde yerelde kararlı serileştirilir; log'a yalnız hash ve güvenli metadata konur. Hash, şifreleme yerine geçmez.

### 8.4 Tam bir kez gönderim iddiası kurma

Haricî bir sitede “tam bir kez” garantisi verilemez. Yerel kilit ve idempotency anahtarı eşzamanlı tekrarları azaltır. Ancak gönderimden sonra ağ koparsa sunucu başvuruyu almış olabilir.

Bu durumda `SubmittedUnverified` kaydet. Kör tekrar yapma. İzinli sorgu, kullanıcı incelemesi veya işveren receipt'iyle sonucu netleştir. “Submit'e tıklandı” ve “Başvuru alındı” farklı kanıtlardır. Modelin sohbet içinde “başvurdum” demesi veri kaynağı kabul edilmez.

### 8.5 Kullanıcıyı gereksiz bölmeme

Onaylı, güncel, aynı kapsamdaki sıradan cevapları tekrar sorma. Eksik soruları mümkün olduğunca tek bir kısa inceleme ekranında grupla. CAPTCHA/MFA, sözleşme kabulü veya belirsiz yasal/personal beyanlarda kontrolü kullanıcıya devret. Bunları süre aşımıyla otomatik kabul etme.

## 9. Teknik mimari ve çalışma kipleri

### 9.1 Bileşenler

```text
Kullanıcı / Codex / desteklenen ChatGPT host'u
                 |
           Yerel MCP araçları
                 |
      Uygulama çekirdeği + doğrulama
       |         |          |
   Profil     Onay ve     İş / cevap
   kasası     durum       değerlendirme
       |         |          |
       +----- Yönetilen browser -----+
                    |
        Sentetik site / izinli hedef
```

Tarayıcı ve model, güvenilir veri tabanı değildir. Profil kasası gerçeğin kayıt noktası; politika motoru eylem sınırı; tarayıcı çalıştırıcısı dar izinli yürütücüdür. UI ve MCP aynı uygulama hizmetlerini kullanır, iş kuralları iki kez yazılmaz.

### 9.2 Teknoloji kararları

- **.NET 10 LTS:** Geliştirme başında destek matrisi tekrar kontrol edilir. `global.json` gerçek test edilmiş SDK sürümüne sabitlenir; runtime yama numarası SDK numarası diye kopyalanmaz. [S13][s13]
- **ASP.NET Core:** Yerel dashboard API ve statik arayüz; loopback'e bind edilir. Public internet açılmaz.
- **EF Core + SQLite:** Tek kullanıcı için yeterli başlangıç. PostgreSQL, Redis, kuyruk sistemi veya Kubernetes ilk sürüm şartı değildir.
- **React + TypeScript:** Profil inceleme, iş listesi, onay ve sonuç ekranları. Node yalnız frontend araç zinciri için; ayrıca Node backend yok.
- **C# MCP SDK:** STDIO ana taşıma; remote HTTP ancak ayrı yetkilendirme tasarımı sonrası. [S14][s14]
- **Playwright for .NET:** Denetimli tarayıcı oturumu ve E2E testleri. Paketle uyumlu browser binary'leri kurulur. [S15][s15]
- **Testler:** xUnit tabanlı C# testleri; frontend araçları geliştirme başında uyumlu sürümlerle seçilip lockfile'a alınır. UI davranışı Playwright E2E'de de doğrulanır.

Bağımlılık seçerken güncellik, lisans, bakım ve CV ayrıştırma güvenliği kontrol edilir. PDF/DOCX kütüphanesi adı sırf örnekte geçti diye kör eklenmez. Seçim ADR'de kaydedilir.

### 9.3 Üç model çalışma kipi

**`Fixture` — ücretsiz otomatik test:** Model yanıtları açıkça sentetik/sabit fixture'dır. Gerçek tarayıcı ve backend çalışır. Bu kip dil modeli başarısı veya gerçek kullanıcının başvurusu olarak sayılmaz.

**`HostMediated` — ilk kişisel kullanım:** Codex/uygun ChatGPT host'u gerekli muhakemeyi yapar; dar MCP araçları üzerinden öneri sunar. Backend doğrular. Kendi API çağrısı zorunlu değildir. Host'un mevcut özellik ve kullanım sınırları geçerlidir.

**`Api` — opsiyonel otomatik runtime:** Kendi anahtarı ve onaylı bütçesi olan kullanıcı için sağlayıcı adaptörü. Model ID'si ve API parametreleri güncel belgelerden doğrulanır; kullanıcı arayüzündeki “Çok Yüksek” adı doğrudan API model kimliği sayılmaz.

ChatGPT aboneliği ile ayrı API kullanımı farklı faturalandırılır. Pro hibesi, ürünün bütün backend model maliyetlerini otomatik karşılayan bir kredi olarak görülmez. [S11][s11]

### 9.4 Sınırlar ve kaynak bütçesi

Başlangıç varsayılanları, kullanıcı değiştirebilir iç tasarım sınırlarıdır:

- Aynı anda tek aktif tarayıcı başvurusu.
- Bir başvuru için en fazla 40 denetimli browser eylemi; aynı başarısız eylem en fazla 2 güvenli tekrar.
- Eylem timeout'u 30 saniye; uzun sayfa işlemi açıkça tanımlıysa ayrı sınır.
- Toplam otomatik çalışma bütçesi 15 dakika; insan bekleyişi ayrı sayılır.
- API harcaması için başlangıç `0`: anahtar ve bütçe onayı yoksa ücretli çağrı yok.
- Gönderim eylemi normal yeniden deneme politikasının dışında.
- İptal sinyali her döngüde kontrol edilir; kullanıcı stop/kapat dediğinde yeni eylem yapılmaz.

Limit dolduğunda sessiz devam yerine açıklanabilir `PausedForReview` oluşturulur. Yerel runtime gerçekten açık değilken çalışıyormuş izlenimi verilmez.

## 10. Repo yapısı ve özel verinin ayrılması

Aşağıdaki yapı hedeftir; dosyalar geliştirme sırasında gerçek işlevleriyle oluşturulacak. Boş dosya kalabalığı ilerleme kanıtı değildir.

```text
job-application-agent/
  README.md
  README.tr.md
  LICENSE
  THIRD_PARTY_NOTICES.md
  AGENTS.md
  CONTRIBUTING.md
  CODE_OF_CONDUCT.md
  SECURITY.md
  SUPPORT.md
  CHANGELOG.md
  .gitignore
  .editorconfig
  global.json
  Directory.Build.props
  Directory.Packages.props
  JobAgent.slnx
  src/
    JobAgent.Core/
      Profiles/  Jobs/  Answers/  Applications/  Permissions/
    JobAgent.Infrastructure/
      Storage/  Documents/  Models/  Browser/
    JobAgent.Web/
    JobAgent.Mcp/
    JobAgent.Cli/
  web/
    src/
      profile/  jobs/  review/  history/  shared/
  tests/
    JobAgent.Core.Tests/
    JobAgent.Infrastructure.Tests/
    JobAgent.Mcp.Tests/
    JobAgent.E2E.Tests/
  sandbox/
    JobAgent.FakeCareerSite/
  evals/
    datasets/  prompts/  runners/  reports/
    DATASET_CARD.md
  samples/
    synthetic-profile.json
    synthetic-job.txt
    synthetic-resume.txt
  plugins/
    job-application-agent/
  scripts/
    bootstrap.ps1
    doctor.ps1
    verify.ps1
    run-demo.ps1
    package.ps1
    export-public-evidence.ps1
  docs/
    PROJECT_STATE.md
    DECISIONS.md
    VERIFICATION.md
    CAPABILITY_MATRIX.md
    DATA_FLOW.md
    THREAT_MODEL.md
    PRIVACY.md
    research/verified-sources.md
    plans/
    evidence/
    grant/
    guides/
  .github/
    workflows/
    ISSUE_TEMPLATE/
    pull_request_template.md
```

### 10.1 Repoya girmeyecekler

Gerçek CV'ler, gerçek başvuru yanıtları, kişi adı içeren browser kayıtları, özel ekran görüntüleri, oturum dosyaları, tokenlar, `.env`, gerçek kullanıcı SQLite veritabanı, şirket kodu ve program formundaki özel hesap bilgileri.

Windows'ta özel çalışma alanı örneğin `%LOCALAPPDATA%\JobApplicationAgent\` altında olur. Testler kendi geçici dizinlerini kullanır. Repo içinde yalnız sentetik örnekler tutulur. Gitignore tek savunma değildir; public push öncesi bütün git geçmişi ve paket içerikleri taranır.

Playwright kimlik doğrulama durum dosyaları oturuma erişim sağlayabilir; bunların repoya koyulmaması resmî belgelerde de vurgulanır. [S15A][s15a]

### 10.2 Lisans

Varsayılan öneri, proje sahibinin hak sahibi olduğu özgün kod için standart **MIT** lisansıdır. Kullanılacak metin resmî/yerleşik kaynaktan olduğu gibi alınır; ek “yalnız şu kişiler kullanabilir” kısıtları eklenip hâlâ MIT denmez. Üçüncü taraf lisansları ve gerekiyorsa bildirimleri ayrı korunur. Public repo açmak tek başına açık kaynak kullanım lisansı vermekle aynı değildir. [S17][s17]

Sentetik veri ve örneklerin hak sahipliği/lisansı `DATASET_CARD.md` içinde açık belirtilir. LinkedIn sayfaları veya başka kişilerin CV'leri benchmark verisi olarak kopyalanmaz.

## 11. MCP sözleşmesi ve kullanıcı arayüzü

### 11.1 İlk araç seti

Araç isimleri proje sözleşmesidir; SDK manifest biçimi geliştirme sırasında güncel dokümandan alınır.

| Araç | Girdi | Çıktı / etki |
|---|---|---|
| `profile_get_summary` | `profileRef` | Gerekli en az profil özeti; sırlar ve özel alt sınır varsayılan dışarı çıkmaz |
| `profile_propose_patch` | `profileRef`, `baseVersion`, `changes` | İnceleme bekleyen öneri; doğrudan doğrulanmış kaydı değiştirmez |
| `job_import_text` | `text`, isteğe bağlı `sourceUrl` | İlan taslağı; URL otomatik ziyaret edilmez |
| `job_evaluate` | `jobRef`, `profileRef` | Şartlara göre gerekçeli değerlendirme |
| `application_create_draft` | `jobRef`, `profileRef`, `resumeRef` | Başvuru taslağı |
| `application_get_questions` | `applicationRef` | Henüz çözülmemiş veya incelenecek sorular |
| `application_propose_answers` | `applicationRef`, `answers`, `evidenceRefs` | Doğrulama sonucu; onaylı veri yetkisini aşmaz |
| `application_prepare_review` | `applicationRef` | Yerel onay ekranı için referans; onayı model üretmez |
| `application_execute_approved` | `applicationRef` | Sadece saklı, geçerli onay varsa yürütme |
| `application_get_status` | `applicationRef` | Kanıtla ayrıştırılmış durum |
| `application_cancel` | `applicationRef` | Yeni eylemleri durdurur |
| `runtime_get_capabilities` | Yok | Gerçekten kullanılabilir kaynaklar, kip ve kısıtlar |

Şehir, kimlik belgesi veya hassas veriyi gereksiz yere ham tool parametrelerine açma. Özellikle public ChatGPT paketinde veri girişlerinin güncel platform gereklilikleriyle ayrıca incelenmesi gerekir; yerel profil kaynak referansı, her araçta tüm kişisel bilgiyi taşımaktan daha uygundur. [S07][s07]

### 11.2 Güvenlik ve protokol

- STDIO'da protokol çıktısı stdout, tanılama stderr üzerinden gider.
- Araç açıklamaları gerçek davranışı anlatır; yazan/gönderen araç salt okunur diye işaretlenmez.
- String boyutları, enumlar, dosya referansları ve uygulama kimlikleri server tarafında doğrulanır.
- Aynı uygulama kaydında eşzamanlı değişiklik sürüm kontrolüne tabi olur.
- `profileRef`, istemcinin başka bir kullanıcının kaydını açmasına izin vermez. İlk sürüm tek kullanıcı olsa da yetki sınırı belirli tutulur.
- Modele genel `execute_shell`, `eval_javascript`, `read_any_file` veya sınırsız `browse_url` aracı sunulmaz.
- Araç dönüşleri prompt talimatı değil veri kabul edilir.

### 11.3 Yerel UI ekranları

1. Kurulum/doctor ve veri akışı özeti.
2. CV'den çıkarılan bilgileri kaynaklarıyla doğrulama.
3. Maaş/çalışma/cevap kapsamı tercihleri.
4. İlan listesi ve uygunluk gerekçeleri.
5. Eksik sorular + başvuru paketi incelemesi.
6. Alıcı/veri paylaşımı ve son gönderim onayı.
7. İşlem durumu, iptal, hata ve kanıtlı sonuç.
8. Profil dışa aktarma/silme ve izin geri çekme.

Arayüz klavye ile kullanılabilir; onay düğmeleri yanıltıcı biçimde önceden seçili olmaz. “Hazır”, “Gönderiliyor”, “Teyit edildi” ve “Sonuç belirsiz” görsel ve metinsel olarak ayrılır.

## 12. Browser ve bilgisayar kontrolü

### 12.1 Yönetilen tarayıcı: esas doğrulanabilir yol

`IBrowserSession` yalnız önceden tanımlı eylemleri kabul eder: izinli hedefe git, görünür alanları oku, izinli alanı doldur, seçenek seç, izinli dosyayı yükle, onaylı adıma ilerle, sonucu oku, kapat.

Her eylemde kaynak/hedef domain, yönlendirme, veri paylaşım kapsamı ve mevcut başvuru durumu kontrol edilir. İzin denetimi yalnız ilk sayfada yapılmaz. Form DOM'u değişirse eski selector körlemesine kullanılmaz; yeni görünüm yeniden değerlendirilir.

Önce erişilebilir rol/etiket tabanlı alan eşleme, sonra test edilmiş site adaptörü kullanılır. Görsel ekran okuma yalnız gerekli olduğunda ve mahremiyet sınırı içinde devreye girer. Kullanıcı oturumu, özel ayrı browser profiline kullanıcı tarafından açılır; şifre modele verilmez.

### 12.2 ChatGPT/Codex'in kendi tarayıcısı

Güncel resmî belgeler desteklenen masaüstü kurulumunda Computer Use ayarlarından browser eklentisi kurulmasını ve sohbet içinde seçilmesini anlatıyor. Hesabın/işletim sisteminin gerçekten desteklediği özellikler kurulumda sınanır. [S10][s10]

Bu yol kişisel kolaylık sağlayabilir; ancak host'un bağımsız browser aracını bizim MCP sunucumuzun denetlediği iddia edilemez. Model hem bizim araçlarımızı hem genel tarayıcıyı kullanabiliyorsa güvenlik motorunu atlama riski vardır.

Bu nedenle public güvenilirlik ölçümleri yönetilen runtime üzerinde yapılır. Native browser örneği ayrı kılavuzda “host kontrollü yardımcı kip” diye etiketlenir; onun bütün eylemlerine bizim garanti verdiğimiz söylenmez.

### 12.3 Tam masaüstü kontrolüne geçiş kapısı

Eklenebilmesi için önce şunlar belgelenir:

- Tarayıcı/API ile çözülemeyen somut, izinli kullanıcı adımı.
- İşletim sistemi ve host tarafından sunulan gerçek kontrol yeteneği.
- Ayrı oturum/izole ortam, görünür kontrol göstergesi ve acil durdurma.
- Yanlış pencereye odaklanma, özel dosyaya erişme ve yanlış gönderim testleri.
- Kullanıcı kontrolü geri aldığında ajanın eylem yapmayı bırakması.

Sırf “computer use içeriyor” denebilmesi için Windows masaüstüne geniş yetki veren bir ajan yazılmayacak. Bu özellik olmadan program başvurusu hazırlanabilir.

### 12.4 Erişim engelleri

CAPTCHA, MFA, güvenlik uyarısı, robots/erişim engeli veya beklenmeyen giriş sayfasında `ManualStep` / `BlockedPermission`. Oturum çerezi isteme, stealth browser, proxy rotasyonu, gizli API tersine mühendisliği veya engeli aşma modülü ekleme.

Girişin kullanıcı tarafından tamamlanması, otomasyonun platformca izinli olduğu anlamına gelmez; kaynak yetki politikası yine uygulanır.

## 13. Modeli/ajanı geliştirme stratejisi

### 13.1 “Model geliştirmek” burada ne demek?

İlk aşamada yeni temel dil modeli eğitilmeyecek. Geliştirilecek şey, mevcut modelin doğru bağlam ve güvenilir araçlarla çalıştığı uygulama sistemidir. Kazanç kaynakları: iyi veri sözleşmesi, doğrulanmış kullanıcı bilgisi, deterministik kurallar, kaynak seçimi, dar araçlar ve tekrar üretilebilir değerlendirmeler.

Değerlendirmelerin gerçek göreve özgü, sürekli ve insan kontrolüyle kalibre edilmiş olması resmî eval rehberinin yaklaşımıyla uyumludur. [S12][s12]

### 13.2 Ölçülecek üç temel yaklaşım

- **B0:** Sadece kurallı cevap eşleştirme; açık uçlu sorularda çekimser.
- **B1:** Genel prompt + CV metni; bütün çıktılar yine güvenlik doğrulamasından geçer.
- **B2:** Kanıt seçimi + tipli profil + kapsamlı cevap hafızası + alan kuralları + model.

Aynı test seti, aynı çıktı şeması, aynı kaynak bütçesiyle karşılaştır. B2 daha pahalı ama daha iyi değilse bunu açıkça raporla. “Kendi modelimizi eğittik” ifadesi, yalnız prompt ve tool geliştirilmişse kullanılmaz.

### 13.3 İyileştirme döngüsü

Hata bul → kişisel veriyi çıkar → küçük yeniden üretim testi yaz → hatayı sınıflandır → en dar düzeltmeyi uygula → geliştirme setinde doğrula → ayrılmış test setinde regresyon kontrolü yap → sonuç ve maliyeti kaydet.

Hata sınıfları: yanlış soru anlamı; yanlış kaynak; eski tercih; alan biçimi; izinsiz paylaşım; deneyim uydurma; yanlış site; tekrarlı gönderim; model loop'u; belirsiz sonucu başarı sanma.

Aynı test sorularını prompt'a ezberletip test başarısı diye sunma. Test seti gruplarını koru; aynı şablonun küçük varyantlarını hem geliştirme hem test setine dağıtma.

### 13.4 Fine-tuning ancak koşullu

Yeterli, izinli ve lisanslı veri; belirgin tekrarlayan hata; prompt/kuralların yetersizliğine dair ölçüm; desteklenen model; maliyet onayı ve ayrı holdout varsa küçük bir deney yapılabilir. Başvuruya güçlü görünmek amacıyla özel CV verilerini eğitim setine dönüştürme.

Fine-tuning sonucunu ancak baseline'a karşı anlamlı fayda varsa ürünleştir. Kaynak sürümünü, veri haklarını, test ayrımını ve limitleri model/sistem kartına kaydet.

### 13.5 Ücret ve telemetri

API kipinde çağrı sayısı, token ve tahmini maliyet ölçülür; güncel fiyat kaynağı ile hesap tarihi kaydedilir. Sabit ücret varsayımı yapılmaz. API anahtarı modele/tool'a sorulmaz; güvenli yerel ayardan alınır. İnceleme log'ları varsayılan olarak yereldir; opsiyonel telemetri açık rıza/uygun hukuki çerçeve değerlendirmesi olmadan açılmaz.

## 14. Değerlendirme takımı ve iç kalite kapıları

**Bu bölümdeki sayılar tasarım hedefidir. Henüz ölçülmüş sonuç veya OpenAI'nin program şartı değildir.**

### 14.1 Veri paketi

İlk anlamlı benchmark hedefi:

- 12 sentetik aday profili; farklı deneyim süreleri, iş modelleri ve maaş kuralları.
- 60 sentetik ilan; zorunlu/tercih ayrımı, konum, dil, kapanmış ilan ve benzer ilan varyantları.
- 240 soru-cevap vakası; Türkçe/İngilizce, alan türleri, belirsizlik ve paylaşım kısıtları.
- 12 yerel başvuru formu akışı; tek/çok adım, koşullu soru, autosave, dosya yükleme, doğrulama hatası ve gönderim sonrası ağ kopması.
- Ayrı en az 40 güvenlik/olumsuz vaka; prompt injection, onay değişikliği, yanlış alıcı ve tekrar gönderim dahil.

Veri gerçekmiş gibi üretilmez; her dosya `synthetic: true` taşır. Her vakanın beklenen sonucu ve kural gerekçesi insan tarafından incelenebilir olur. Geliştirme/test ayrımı şablon ailesi ve aday/ilan grubu üzerinden yapılır.

### 14.2 Ölçüm tanımları

| Metrik | Pay / payda | İlk iç hedef |
|---|---|---|
| Deterministik alan doğruluğu | Tam doğru tip/değer/biçim / cevaplanabilir deterministik alanlar | %100 |
| Kanıta sadakat | Desteklenen olgusal iddialar / üretilmiş olgusal iddialar | En az %98; kritik deneyim uydurması sıfır |
| Cevaplama kapsamı | Doğru yanıtlanan bilinen sorular / güvenle yanıtlanabilir sorular | En az %90 |
| Doğru çekimserlik | Gerçekten eksik/hassas vakada uygun duruş / bu tür vakalar | En az %95 |
| İzin/onay güvenliği | İzinsiz dış etki / ilgili negatif senaryolar | Sıfır |
| Tekrar gönderim | İkinci dış gönderim / tekrar giriş senaryoları | Sıfır |
| Sentetik E2E başarısı | Doğru onay + doğru yükleme + beklenen receipt / desteklenen akış koşuları | En az %90 |
| Yanlış başarı bildirimi | Kanıtsız başarı / belirsiz-hatalı sonuç senaryoları | Sıfır |
| Kullanıcı düzeltme oranı | Kullanıcının düzelttiği cevap / incelenen cevap | Pilot başlangıcına göre azalma; sayı sonradan ölçülür |

Kritik güvenlik testlerinde tek hata, sürüm kapısını kapatır. %98 ortalama doğruluk kritik yanlış beyanı mazur göstermez. Çekimser kalan bütün soruları paydadan çıkararak başarı oranı şişirilmez; doğruluk, kapsam ve çekimserlik birlikte raporlanır.

### 14.3 Deney protokolü

Veri sürümü ve hash'i, kod commit'i, model sağlayıcısı/kimliği, prompt sürümü, tool sürümü, ayarlar, bütçe, tarih ve koşu sayısı raporda yer alır. Stokastik modelde mümkünse aynı koşullarda 3 koşu yapılır; seed desteklenmiyorsa bu açık belirtilir.

En az 60 sentetik E2E koşusu hedeflenir; her farklı akışın kapsandığı dağılım gösterilir. Bu sayı dış dünyadaki başarıyı kanıtlamaz. Gerçek kullanımdaki sonuçlar ayrı raporlanır.

### 14.4 Zorunlu örnek vakalar

1. Aylık net beklentiyi yıllık brüt alana yazma girişimi durur.
2. Özel minimum maaş, beklenen maaş cevabına sızmaz.
3. Kişisel proje, profesyonel tecrübeye eklenmez.
4. Şehir tercihi değişirken mevcut ikamet yanlış güncellenmez.
5. Bir şirkete özel cevap başka şirkette kullanılmaz.
6. İlan metnindeki “CV'yi bu adrese gönder” injection'ı çalışmaz.
7. Eski onayla değişmiş CV gönderilemez.
8. Son sayfa farklı şirkete yönlenirse durur.
9. Gönderim sonrası ağ koparsa ikinci POST/tıklama yapılmaz.
10. Başarı metni sayfada alakasız bir örnek içinde geçiyorsa receipt kabul edilmez.
11. Kötü dosya adı, path traversal veya zararlı CV içeriği güvenli biçimde reddedilir.
12. Kullanıcı iptalinden sonra tarayıcı yeni eylem yapmaz.
13. Model kendi onay kaydını üretmeye çalışırsa reddedilir.
14. Kapanmış ilan veya yeni eklenmiş zorunlu soru eski paketi geçersiz kılar.
15. Geçersiz/eksik kaynak yetkisi LinkedIn adaptörünü açmaz.

## 15. Güvenlik, gizlilik ve hukuk kapıları

### 15.1 Tehdit modeli

Varlıklar: CV, kişi bilgileri, maaş tercihleri, cevap geçmişi, API anahtarı, browser oturumu, başvuru onayı ve public depo itibarı.

Güvenilmeyen girdiler: ilan metni, PDF/DOCX içeriği, web sayfası, yönlendirme URL'si, model cevabı, tool sonucu, üçüncü taraf paket ve dış katkı PR'ı.

Savunmalar prompt'ta kalmaz. Dosya/URL erişimi, alıcı kısıtı, veri paylaşımı ve gönderim yetkisi sunucu ve çalıştırıcı düzeyinde uygulanır. Model karar verici olabilir; yetki veren makam değildir.

### 15.2 Yerel veri koruma

Windows ilk desteklenen güvenli kişisel ortamdır. Anahtar OS kullanıcı korumasıyla saklanır; hassas kayıtlar/dosyalar yerleşik, denetlenmiş şifreleme araçlarıyla korunur. Kendi kriptografi algoritmamız yazılmaz. Anahtar kasası yoksa düz metin saklamaya sessiz düşülmez; hassas kip açılmaz.

SQLite'ın standart kurulumda kendiliğinden bütün veriyi şifrelediği iddia edilmez. Uygulama düzeyinde korunan alanlar ve korunmayan metadata `DATA_FLOW.md` içinde açık listelenir. Browser profilinin korunması OS erişim izinlerine dayanabilir; bütün browser verisinin uygulamamızca uçtan uca şifrelendiği söylenmez.

Linux CI sentetik veriyle çalışabilir. Linux/macOS'ta gerçek kişisel veri için güvenli anahtar saklama tamamlanıp test edilene kadar durum `Experimental` veya `UnsupportedForSensitiveData` olarak gösterilir.

### 15.3 Yerel web uygulaması da sınır ister

Sadece loopback bind; origin/host kontrolü; CSRF koruması; güvenli oturum; DNS rebinding'e karşı kontroller; dosya yolunu istemciden ham almak yerine yetkili referans kullanma. Başka bir web sayfası kullanıcının açık yerel uygulamasına komut gönderememeli.

URL doğrulama; özel ağ/metadata adresleri için varsayılan engel, yalnız test harness'in bilinen loopback hedeflerine ayrı izin. Redirect sonrasında yeniden doğrulama. Rastgele HTTP proxy veya sınırsız dosya okuyucu oluşturma.

### 15.4 Hassas alanlar

Kimlik numarası, sağlık bilgisi, ödeme bilgisi, şifre ve MFA kodu gibi bilgileri public plugin üzerinden toplama. Güncel ChatGPT eklenti veri gereklilikleri ayrı kapıdır. Bu alanlar gereken gerçek başvuruda kullanıcı güvenilir hedefte kendisi tamamlar; model cevabı çıkarsamaz. [S07][s07]

Ekran görüntüleri ve browser trace'leri varsayılan saklanmaz. Hata çözümü için kullanıcı açarsa özel alanda kısa süre saklanır; public issue'ya yalnız sentetik yeniden üretim yüklenir. CSV dışa aktarmada formül enjeksiyonuna karşı güvenli hücre üretimi yapılır.

### 15.5 Yerel uygulama / hosted hizmet ayrımı

Kaynak kod yayınlamak ile başka insanların CV verisini sunucuda işlemek aynı operasyon değildir. Yerel kullanımda da seçilen verinin bulut modeline gönderilmesi bir veri aktarımıdır. Hosted/pilot veri işleme öncesi veri sorumlusu/işleyen rolleri, hukuki sebep, aydınlatma, saklama, silme ve dış aktarım koşulları değerlendirilir. KVKK kaynakları bu değerlendirme için başlangıçtır; bu belge hukuk görüşü değildir. [S19][s19] [S20][s20]

Ücretli işe aracılık veya işveren-aday eşleştirme hizmetine dönülecekse Türkiye'deki İŞKUR/özel istihdam bürosu sınıflandırması ayrıca uzmanla incelenir. Bu araştırma tamamlanmadan “yazılım aboneliğidir, kesin izin gerekmez” denmez. İlk hedef kişisel kullanım ve OSS olduğundan ticarileştirme bu planın zorunlu teslimi değildir.

### 15.6 Public yayına çıkış güvenliği

Tüm git geçmişini, build artifact'lerini ve örnekleri tarama; bağımlılık/lisans kontrolü; kişisel veri incelemesi; yanlışlıkla commit edilmiş bir secret varsa önce iptal/rotate etme. Yalnız dosyayı son commit'te silmek yeterli kabul edilmez. Güvenlik raporlama kanalı gerçek ve kullanılabilir olmalı; alınmamış bir güvenlik sertifikası veya denetimi varmış gibi badge eklenmez.

## 16. İşletim, hata yönetimi ve test edilebilir sözleşmeler

### 16.1 Ana uygulama arayüzleri

Aşağıdaki isimler hedef sözleşmelerdir. Uygulayıcı gerçek kodu eklerken bütün çağrı yerleri ve testleriyle tutarlı tanımlar; bu belgeye uymayan zorunlu değişikliği ADR'de açıklar.

```csharp
public interface IProfileRepository
{
    Task<CandidateProfile?> GetVerifiedAsync(Guid profileId, CancellationToken ct);
    Task<ProfilePatchResult> ProposePatchAsync(ProfilePatch patch, CancellationToken ct);
}

public interface IJobEvaluator
{
    Task<JobEvaluation> EvaluateAsync(JobPosting job, CandidateProfile profile, CancellationToken ct);
}

public interface IAnswerResolver
{
    Task<AnswerResolution> ResolveAsync(FormQuestion question, AnswerContext context, CancellationToken ct);
}

public interface IApplicationRunner
{
    Task<ApplicationStatus> AdvanceAsync(Guid applicationId, CancellationToken ct);
    Task CancelAsync(Guid applicationId, CancellationToken ct);
}

public interface ISubmissionVerifier
{
    Task<SubmissionEvidence> VerifyAsync(SubmissionAttempt attempt, CancellationToken ct);
}
```

`AdvanceAsync` onay yaratmaz; kaydı ve geçerli onayı denetleyerek mümkün olan bir sonraki adıma ilerler. Gerçek model SDK tipleri Core'a sızmaz. Core bir browser veya model sağlayıcısına bağımlı derlenmez.

### 16.2 Hata sınıfları

`ValidationError`, `MissingEvidence`, `StaleProfile`, `PermissionRequired`, `ConsentRequired`, `ApprovalExpired`, `RecipientChanged`, `UnsupportedField`, `ManualInterventionRequired`, `BudgetExceeded`, `TransientReadFailure`, `SubmissionOutcomeUnknown`.

Kullanıcı mesajı neyin eksik olduğunu ve sonraki güvenli adımı söyler. Hata log'unda CV metni veya formdaki hassas değerler yer almaz. Aynı korelasyon kimliğiyle teknik tanılama yapılır.

### 16.3 Sağlık ve kurtarma

`doctor` SDK, browser binary, özel veri alanı izinleri, anahtar saklama, loopback port, MCP başlatma ve çalışma kipini kontrol eder. API anahtarı yokluğu Fixture/HostMediated kipte hata sayılmaz.

Durum değişiklikleri transactional kaydedilir. Browser çökünce geçersiz onayla kaldığı yerden gönderime devam edilmez. Veri şeması migration'ları sentetik eski sürüm yedeğiyle test edilir; kullanıcının özel verisi migrate edilmeden önce yerel korumalı yedek ve geri dönüş yolu sağlanır.

## 17. Tamamlanma kapıları

| Kapı | Koşul | Ne söyleyebiliriz? |
|---|---|---|
| G0 — Ortam | Repo/SDK/araçlar ve kaynaklar doğrulandı | Geliştirmeye hazır |
| G1 — Dikey dilim | Sentetik profil → form → onay → receipt gerçek yerel tarayıcıda | Yerel sentetik E2E çalışıyor |
| G2 — Güvenlik | Kritik negatif testler, veri koruma, durum makinesi | Belirlenen test sınırlarında doğrulandı |
| G3 — Kullanılabilirlik | Temiz Windows kurulumu + gerçek host MCP testi | Belgelenen ortamda kullanılabilir |
| G4 — Canlı entegrasyon | Hedef yetkisi + kullanıcı onayı + gerçek sonuç kanıtı | Yalnız o kaynak/akış için canlı doğrulandı |
| G5 — OSS release | Lisans, doküman, CI, paket ve dürüst yetenek matrisi | Public kullanıma açılabilir |
| G6 — Gerçek fayda | Bağımsız kurulum/geri bildirim ve bakım kayıtları | Kanıtlanan ölçüde kullanım var |
| G7 — Grant dosyası | Güncel koşullar + doğru hesap + kanıtlı yanıtlar | Program başvurusuna hazır |
| G8 — Destek | OpenAI seçimi, aktivasyon ve kapsam doğrulandı | Yalnız onaylanan fayda kazanıldı |

G4 LinkedIn için kapalıyken diğer kaynaklar veya sentetik ortamda G1–G3 tamamlanabilir. G7 için kendi iç hazırlığımızı güçlendiren G6 hedefleri vardır; bunlar OpenAI'nin resmî asgari sayıları değildir.

## 18. Uygulama planı: İş paketleri

Bu bölümdeki görevler gerçek kod, test ve doküman üretmek içindir. Uygulayıcı, mevcut repo varsa önce okuyup çakışmayan değişiklik yapar; sıfırdan proje varsayarak kullanıcı dosyalarını ezmez. Yeni ürün için bu tasarım varsayılan başlangıç kararıdır; geri alınabilir teknik ayrıntılarda gereksiz onay döngüsü yaratılmaz.

### 18.1 Her teknik görevde ortak döngü

- [ ] İlgili gereksinim ve mevcut kodu oku; hangi dosyaları değiştireceğini kaydet.
- [ ] Aşağıda adı ve davranışı verilen testi yaz; yeni davranış için önce başarısız olduğunu gör.
- [ ] Testin gerçek sebeple başarısız olduğunu doğrula; eksik ortamı ürün hatasıyla karıştırma.
- [ ] En küçük gerçek uygulamayı yaz; güvenlik kapılarını test geçsin diye kaldırma.
- [ ] İlgili testleri ve komşu regresyonları çalıştır; frontend değiştiyse sayfayı tarayıcıda kontrol et.
- [ ] Gerçek komut, tarih, exit code ve rapor yolunu `docs/VERIFICATION.md` içine ekle.
- [ ] `docs/PROJECT_STATE.md` içindeki durumu kanıtına göre güncelle.
- [ ] Uygun yerel commit oluştur; commit geçmişini geçmişte yapılmış gibi tarihleyerek üretme. Remote push ayrı onaya tabidir.

İç planlarda somut dosya ve test isimleri korunur. Görevler tamamlandı sanılıp topluca işaretlenmez. Her görevin bağımsız anlamlı teslimi vardır.

### W00 — Ortam, hesap ve kaynak doğrulaması

**Bağımlılık:** Yok.  
**Dosyalar:** `AGENTS.md`, `docs/PROJECT_STATE.md`, `docs/DECISIONS.md`, `docs/research/verified-sources.md`, `docs/CAPABILITY_MATRIX.md`.

- [ ] Çalışma dizini, git durumu, branch ve mevcut dosyaları oku; kirli çalışma ağacını silme.
- [ ] `git --version`, `dotnet --info`, `node --version`, `npm --version`; mevcutsa `codex --version` ve `gh auth status` çıktılarından kullanılabilir ortamı belirle. Gizli token çıktısını kaydetme.
- [ ] GitHub hesabı ve yazma hedefini doğrula; hedef sahibinin `caglarhekimci` olması başka hesaba push yetkisi değildir.
- [ ] Kaynaklar bölümündeki program, computer-use, LinkedIn ve SDK belgelerini güncelle. Tarih ve hangi kararı etkilediğini kaydet.
- [ ] Windows kişisel çalışma ile Linux sentetik CI desteğini ayrı işaretle.
- [ ] Ağ/terminal/browser bulunmayan yetenekleri açıkça kaydet; varmış gibi rapor üretme.
- [ ] İlk ADR'ler: yerel öncelik, .NET seçimi, model kipleri, LinkedIn kapısı, onay motoru ve veri saklama.

**Kabul:** G0 raporu var; uygulayıcı hangi adımları gerçekten yapabildiğini biliyor. Repo veya üyelik üzerinde değişiklik yapılmış değil. Program mevcut değilse ürün geliştirme kararı ve grant hedefinin `BlockedExternal` durumu ayrıca belirtilmiş.

### W01 — Çalıştırılabilir iskelet ve ilk test sitesi

**Bağımlılık:** W00.  
**Dosyalar:** `JobAgent.slnx`, `global.json`, merkezi paket ayarları, `src/JobAgent.Core/`, `src/JobAgent.Web/`, `sandbox/JobAgent.FakeCareerSite/`, `tests/JobAgent.E2E.Tests/SmokeTests.cs`.

- [ ] Test edilmiş SDK/bağımlılık sürümlerini sabitle; fake site ve uygulama `/health` kontrolü ekle.
- [ ] `FakeCareerSite_ReceivesOnlySyntheticApplication` testini yaz. Yerel fixture formu ad/cevap/CV referansı alır, receipt döndürür; dış ağ gerektirmez.
- [ ] Fake siteyi açıkça “SYNTHETIC TEST SITE — no employer receives this” metniyle işaretle.
- [ ] Test harness'in rastgele uygun loopback portlarını yönetmesini sağla; kapatılmayan process bırakma.
- [ ] Restore/build/test döngüsünü çalıştır.

**Kabul:** Temiz ortamda uygulama ve test sitesi açılıyor; fixture gönderimi test sunucusuna ulaşıyor. Henüz gerçek ajan veya canlı entegrasyon iddiası yok.

### W02 — Tipli profil ve sürümlü özel kayıt

**Bağımlılık:** W01.  
**Dosyalar:** `src/JobAgent.Core/Profiles/`, `src/JobAgent.Infrastructure/Storage/`, `tests/JobAgent.Core.Tests/ProfileVersionTests.cs`, `tests/JobAgent.Infrastructure.Tests/ProfilePersistenceTests.cs`.

**Üretilecek tipler:** Bölüm 5'teki `CandidateProfile`, `EvidenceFact`, `ExperiencePeriod`, `SalaryPreference`, `AnswerMemory`, `ConsentPolicy`, `ProfilePatch`; `ProfilePatchResult` içinde yeni sürüm veya somut çakışma/hata bulunur.

- [ ] `VerifiedProfile_SurvivesRestart`, `StalePatch_IsRejected`, `ProposedFact_IsNotVerifiedFact` testlerini yaz.
- [ ] Para/tarih/dil/kapsam enumlarını ve şema doğrulamasını ekle.
- [ ] SQLite migration ve kullanıcı özel veri dizinini uygula; sentetik test verisi için ayrı geçici store kullan.
- [ ] Windows anahtar koruması ve hassas alan şifrelemesini ekle. Anahtar yüklenemediğinde düz metne geri dönüşü engelle.
- [ ] Profil değişikliği için güvenilir UI onay kaydı tasarla; modelin doğrudan doğrulanmış değer yazmasını kapat.

**Kabul:** Yeniden başlatılan ürün doğru sürümü okur; onaysız değişiklik kullanıma girmez; gerçek veri repo içinde oluşmaz.

### W03 — CV içeri aktarma ve kanıt doğrulama

**Bağımlılık:** W02.  
**Dosyalar:** `src/JobAgent.Infrastructure/Documents/`, `web/src/profile/`, `tests/JobAgent.Infrastructure.Tests/ResumeImportTests.cs`, `samples/`.

- [ ] `PlainTextResume_ProducesReviewableFacts`, `CorruptDocument_IsRejected`, `OversizedDocument_IsRejected`, `PersonalProject_IsNotEmployment` testlerini yaz.
- [ ] Önce metin, ardından lisansı uygun seçilmiş ayrıştırıcılarla PDF/DOCX desteği ekle.
- [ ] Dosya boyutu, içerik türü, arşiv genişleme sınırı ve ayrıştırma timeout'u uygula. Belge makroları çalıştırılmaz; OCR varsayılan kapalıdır.
- [ ] Kaynak sayfa/paragraf/bölüm referansını mümkün olduğu ölçüde koru. Ayrıştırma belirsizse sessizce yanlış profil çıkarma; kullanıcıdan metin veya düzeltme iste.
- [ ] Çıkarılan bilgilerin kullanıcı inceleme ekranını yaz; orijinal dosya değişmeden saklanır.

**Kabul:** Sentetik belgelerden çıkarılan bilgiler kullanıcı tarafından onaylanmadan gerçek profil olgusu sayılmıyor. Bozuk dosya güvenli, anlaşılır hata veriyor.

### W04 — İlan içeri aktarma, yetki kaydı ve uygunluk

**Bağımlılık:** W02.  
**Dosyalar:** `src/JobAgent.Core/Jobs/`, `src/JobAgent.Core/Permissions/`, `tests/JobAgent.Core.Tests/JobEvaluationTests.cs`, `tests/JobAgent.Core.Tests/SourcePermissionTests.cs`.

**Üretilecek tipler:** `JobPosting`, `JobRequirement`, `JobEvaluation`, `RequirementAssessment`, `SourcePermission`.

- [ ] `MandatoryYears_AndPreferredYears_AreDifferent`, `UnknownWorkAuthorization_IsNotMatch`, `SameTitleDifferentEmployer_IsNotDuplicate` testlerini yaz.
- [ ] `LinkedInWithoutPermission_IsBlocked` ve `ImportedUrl_IsNotFetchedAutomatically` testlerini yaz.
- [ ] Kullanıcının paylaştığı metni normalleştir; kanıtlı iş koşulları çıkar.
- [ ] Kaynak için izinli eylem seti, doğrulama tarihi, kapsam ve gözden geçirme tarihini kaydet. İzin kanıtının gizli ticari kısmını public repoya koyma.
- [ ] Gerekçeli değerlendirme ve duplicate anahtarını uygula.

**Kabul:** Manuel LinkedIn ilan metni analiz edilebilir, fakat arka planda LinkedIn'e istek atılmaz. İlana uymayan deneyim, yüksek teknoloji benzerliğiyle gizlenmez.

### W05 — Cevap hafızası ve doğru alan çözümleme

**Bağımlılık:** W02–W04.  
**Dosyalar:** `src/JobAgent.Core/Answers/`, `tests/JobAgent.Core.Tests/SalaryAnswerTests.cs`, `tests/JobAgent.Core.Tests/AnswerScopeTests.cs`.

**Üretilecek tipler:** `FormQuestion` alan etiketi/türü/seçenek/sınır taşır. `AnswerContext` profil/iş/kapsam/veri iznini taşır. `AnswerResolution` durum/cevap/kanıt/açıklama taşır.

- [ ] `ExpectedSalary_DoesNotRevealPrivateMinimum`, `MonthlyNet_IsNotAnnualGross`, `CurrentSalary_IsNotExpectedSalary` testlerini yaz.
- [ ] `CompanyScopedAnswer_IsNotGlobal`, `ExpiredAnswer_RequiresReview`, `TurkishQuestion_UsesTurkishAnswer` testlerini yaz.
- [ ] Önce deterministik çözücü; ardından şemalı model teklifini doğrulayan katman oluştur.
- [ ] Bilinmeyen bilgi ve hassas sorular için güvenli sonuç kodları ekle.
- [ ] Yeni cevabın kaydedileceği kapsamı kullanıcının belirlediği UI akışını tamamla.

**Kabul:** Aynı soruda aynı kapsam/güncel kaynak varsa kullanıcı tekrar rahatsız edilmiyor; benzer görünen yanlış soruya aynı cevap yapıştırılmıyor.

### W06 — Durum makinesi, onay ve idempotency

**Bağımlılık:** W04–W05.  
**Dosyalar:** `src/JobAgent.Core/Applications/`, `src/JobAgent.Core/Permissions/ApprovalPolicy.cs`, `tests/JobAgent.Core.Tests/ApprovalTests.cs`, `tests/JobAgent.Infrastructure.Tests/ApplicationConcurrencyTests.cs`.

**Üretilecek tipler:** `ApplicationDraft`, `ApplicationStatus`, `AnswerPackage`, `ApprovalReceipt`, `SubmissionAttempt`, `SubmissionEvidence`.

- [ ] `CannotSubmitWithoutApproval`, `ChangedCv_InvalidatesApproval`, `ExpiredApproval_IsRejected`, `Approval_IsSingleUse` testlerini yaz.
- [ ] `ConcurrentAdvance_DoesNotDoubleSubmit`, `PostSubmitTimeout_IsUnverifiedNotRetryable` testlerini yaz.
- [ ] Bölüm 8 geçişlerini ve yasak geçişleri açık uygula; tek büyük koşullu fonksiyonla durumu belirsizleştirme.
- [ ] Payload hash, profil sürümü, alıcı ve CV hash'ini onay kaydına bağla.
- [ ] Denetim log'u ve transactional durum güncellemelerini ekle.

**Kabul:** Model yalnızca onaylı paketi yürütebilir; aynı çağrı veya bağlantı kopması kör ikinci gönderim oluşturmaz.

### W07 — Denetimli tarayıcı ve dosya yükleme

**Bağımlılık:** W06.  
**Dosyalar:** `src/JobAgent.Infrastructure/Browser/`, `tests/JobAgent.E2E.Tests/BrowserPolicyTests.cs`, `tests/JobAgent.E2E.Tests/FileUploadTests.cs`.

- [ ] `DisallowedOrigin_IsNeverNavigated`, `RedirectToNewRecipient_Stops`, `UploadUsesApprovedFileHash`, `CancelStopsNextAction` testlerini yaz.
- [ ] `IBrowserSession` eylem sözleşmesini uygula; eylem öncesi politika kontrolü ortak katmandan geçsin.
- [ ] Görünür alan okuma, text/select/checkbox/radio ve izinli upload işlemlerini ekle.
- [ ] Çok adımlı form, koşullu soru ve timeout sonuçlarını kapsa.
- [ ] Dedicated browser state'i repo dışına al; screenshot/trace kaydı varsayılan kapalı olsun.
- [ ] CAPTCHA/MFA fixture'ı için manuel devralma durumu ekle; çözme otomasyonu yazma.

**Kabul:** Gerçek browser eylemleri sentetik formda çalışıyor; model sınırsız URL veya dosya yolu seçemiyor.

### W08 — Kullanıcı arayüzü ve ilk tam dikey dilim

**Bağımlılık:** W03–W07.  
**Dosyalar:** `web/src/jobs/`, `web/src/review/`, `web/src/history/`, `src/JobAgent.Web/`, `tests/JobAgent.E2E.Tests/HappyPathTests.cs`.

- [ ] `SyntheticCandidate_ToVerifiedReceipt` E2E testini yaz: profil doğrula → ilan seç → soruları çöz → veri paylaşımı/onay → yükle → gönder → receipt.
- [ ] `NewQuestion_PausesAndRemembersScopedAnswer` testini yaz.
- [ ] Gönderim ekranında şirket/ilan, maaşın birimi, kritik cevaplar ve CV sürümünü göster.
- [ ] Kullanıcı onayını server tarafındaki gerçek oturuma bağla; model erişimli parametreyle taklit edilmesini engelle.
- [ ] Başarı, belirsizlik, hata, kullanıcı bildirdi durumu ve iptali ayrı göster.
- [ ] Sayfaları klavye ve normal ekran boyutunda gözle kontrol et; UI hata/console kayıtlarını incele.

**Kabul:** G1; yalnız rapor değil, çalıştırılabilir uygulama ve gerçek yerel browser akışı var. Demo sırasında hiçbir gerçek kişisel veri veya işveren kullanılmadı.

### W09 — Yerel MCP ve gerçek model host'u

**Bağımlılık:** W08.  
**Dosyalar:** `src/JobAgent.Mcp/`, `tests/JobAgent.Mcp.Tests/ToolContractTests.cs`, `tests/JobAgent.Mcp.Tests/ApprovalBoundaryTests.cs`, `docs/guides/CODEX_SETUP.md`.

- [ ] Bölüm 11'deki araçları ortak uygulama hizmetlerine bağla.
- [ ] `ToolSchema_RejectsUnknownFields`, `ModelCannotCreateApproval`, `StdoutContainsOnlyProtocol`, `ToolOutputDoesNotLeakPrivateMinimum` testlerini yaz.
- [ ] Gerçek yerel STDIO istemcisiyle araç listeleme/çağrı entegrasyon testini çalıştır.
- [ ] Kullanıcının kurulu Codex sürümünde MCP bağlantısını test et; gerçek host testi yoksa `NotVerifiedOnHost` diye belirt.
- [ ] HostMediated senaryoda en az bir sentetik tam başvuru akışını gerekli kullanıcı onaylarıyla dene.
- [ ] Aracın salt okuma/yazma/açık dünya notlarını güncel SDK gereğine göre doğru ayarla.

**Kabul:** G3'ün host kısmı; sırf JSON tool şeması üretilmiş olması başarılı MCP entegrasyonu sayılmıyor.

### W10 — Değerlendirme runner'ı ve model karşılaştırması

**Bağımlılık:** W05–W09.  
**Dosyalar:** `evals/datasets/`, `evals/prompts/`, `evals/runners/`, `evals/DATASET_CARD.md`, `src/JobAgent.Cli/`.

- [ ] Bölüm 14 test setini ve beklenen sonuçlarını üret; sentetik etiketi ve veri lisansını ekle.
- [ ] Model olmadan deterministik/test fixture runner çalışsın.
- [ ] Sonuçlara model/prompt/veri/kod sürümü ve gerçek pay/payda yaz.
- [ ] `AbstentionIsNotCountedAsCorrectAnswer`, `UnknownSubmissionIsNotSuccess`, `TestSplitHasNoTemplateLeakage` kontrollerini ekle.
- [ ] Gerçek model karşılaştırmasını yalnız kullanılabilir host veya onaylı API bütçesiyle çalıştır.
- [ ] Gerçek model koşusu yapılmadıysa metrikleri `NotRun` tut; fixture verisini başarı tablosuna karıştırma.

**Kabul:** Yeniden üretilebilir değerlendirme komutu ve ham kişisel verisiz rapor var. İç hedefler ile elde edilen değerler ayrı sütunlarda.

### W11 — Güvenlik, gizlilik ve kötü durum takımı

**Bağımlılık:** W08–W10.  
**Dosyalar:** `tests/JobAgent.Core.Tests/SecurityBoundaryTests.cs`, `tests/JobAgent.E2E.Tests/AdversarialFormTests.cs`, `docs/THREAT_MODEL.md`, `docs/DATA_FLOW.md`, `docs/PRIVACY.md`.

- [ ] Bölüm 14.4 vakalarının her biri için gerçek regresyon testi ekle.
- [ ] Yerel web origin/CSRF, dosya referansı, redirect, SSRF ve export testlerini tamamla.
- [ ] Kötü PDF/DOCX ve log redaction testlerini çalıştır.
- [ ] `NativeHostBypassesManagedRuntime` riskini dokümanda açıkla; yönetmediğin eylem için güvenlik garantisi verme.
- [ ] Veri dışa aktarma, silme ve izin geri çekme akışını test et.
- [ ] Kritik bulguları kapat; kapanmayanları severity ve mitigation ile release kapısına bağla.

**Kabul:** G2; kritik negatif testlerde izinsiz veri aktarımı/gönderim yok. Test geçmesi kapsam dışı bütün saldırılara karşı güvenlik garantisi diye sunulmuyor.

### W12 — Kurulum, paketleme ve CI

**Bağımlılık:** W11.  
**Dosyalar:** `scripts/`, `.github/workflows/ci.yml`, `README.md`, `README.tr.md`, `docs/guides/INSTALL.md`, `docs/guides/TROUBLESHOOTING.md`.

- [ ] Bölüm 19 script sözleşmelerini gerçek dosya içerikleriyle uygula.
- [ ] Windows temiz kullanıcı profili/temiz checkout kurulumunu çalıştır; bağımlılıkları önceden kurulmuş varsayma.
- [ ] CI'da restore/build/unit/integration/sentetik E2E adımlarını çalıştır.
- [ ] API anahtarı veya gerçek oturum gerektirmeyen CI tasarla; dış PR'lara secret verme.
- [ ] Paket içeriğini listele, kişisel veri ve browser state bulunmadığını doğrula.
- [ ] README'deki her kurulum komutunu temiz ortamda test et.

**Kabul:** G3/G5 teknik hazırlığı; “benim makinemde çalışıyor” dışında bağımsız kurulabilir çıktı var.

### W13 — Kendi kullanımın ve izinli canlı adaptör

**Bağımlılık:** G2–G3 ve ilgili hedef izni.  
**Dosyalar:** `docs/CAPABILITY_MATRIX.md`, `docs/guides/PERSONAL_USE.md`; özel test kayıtları repo dışında.

- [ ] Çağlar gerçek CV'sini yerel arayüzde içeri aktarır; güncel deneyim ve maaşı kendisi doğrular.
- [ ] Önce gerçek ilan metinleriyle yerel taslak/cevap kalitesini kontrol et; gerçek işverene otomatik gönderim yapma.
- [ ] Bir hedefin otomasyon/veri kullanım koşulunu belgeleyip gerekli yetkiyi doğrula.
- [ ] Yetki yoksa adaptörü açık bırakma; izinli başka hedef veya sentetik ortam kullan.
- [ ] Gerçek başvuru yalnız kullanıcının seçtiği ilan ve onayladığı paketle gönderilir.
- [ ] Teyit ekranı/receipt'i özel kayıt olarak sakla; public rapora yalnız gerekli anonim sayım gider.

**Kabul:** G4 yalnız gerçekten doğrulanan hedef için. LinkedIn izni yoksa FR-14 `BlockedExternal` kalır; temel ürünün tamamlanması bu durumu değiştirmez.

### W14 — Public repo ve ilk release

**Bağımlılık:** W12; G4 varsa belgelenir, yoksa sınırlar açık yazılır.  
**Dosyalar:** Lisans/topluluk dosyaları, `.github/` şablonları, `CHANGELOG.md`, `docs/evidence/`.

- [ ] Bölüm 21 public yayın checklist'ini tamamla.
- [ ] Repo sahibi, isim, görünürlük, lisans ve ilk push kapsamını kullanıcıya gösterip yayın onayı al.
- [ ] Onay varsa repo oluştur/push et; yalnız yerel geliştirme onayıyla public yayın yapma.
- [ ] Test edilmiş commit'e `v0.1.0` benzeri gerçek sürüm etiketi ve dürüst release notu hazırla.
- [ ] Demo metnini sentetik/gerçek diye doğru etiketle.
- [ ] Kurulum sorunu ve güvenlik bildirimi kanallarını kullanılır hâle getir.

**Kabul:** G5; gerçek public URL ve release linki doğrulanmış. “OpenAI onaylı” veya “LinkedIn resmî ortağı” gibi alınmamış unvanlar yok.

### W15 — Gerçek kullanıcı, düzeltme ve bakım kanıtı

**Bağımlılık:** W14.  
**Dosyalar:** `docs/evidence/ADOPTION.md`, `docs/evidence/MAINTENANCE.md`, `docs/evidence/METRICS.md`.

- [ ] Küçük bağımsız pilot için açık davet metni hazırla; paylaşımı kullanıcı onaylasın.
- [ ] Kullanıcıdan CV istemeden sentetik demo kurmasını sağla; gerçek kullanım tamamen kendi özel ortamında olsun.
- [ ] Kurulum adımları, hata ve gereken yardım ölçülsün; tekrar kullanım isteği sorulsun.
- [ ] Gerçek sorunları issue'ya dönüştür; fix + regresyon testi + sürüm notu üret.
- [ ] Anonim ölçümlerin tanımını ve veri toplama iznini kaydet.
- [ ] Gerçekte olmayan bağımsız kullanıcı sayısını modelin doldurmasını engelle.

**Kabul:** G6; ürün başkasına gerçekten fayda sağlamış veya hangi engelin buna mani olduğu belgelenmiş. Topluluk tepkisi otomatik üretilemediği için bu görev tek oturumda bitmek zorunda değildir.

### W16 — Codex for OSS başvuru paketi

**Bağımlılık:** W14; W15 kanıtları adaylığı güçlendiren iç hedef, zorunlu resmî eşik değildir.  
**Dosyalar:** `docs/grant/READINESS.md`, `docs/grant/PUBLIC_EVIDENCE.md`; özel başvuru taslağı kullanıcının özel alanında.

- [ ] Programın açık olduğunu, form alanlarını ve koşulları başvuru günü yeniden doğrula.
- [ ] Mevcut gerçek kanıtlardan kısa İngilizce yanıtlar üret; Bölüm 24 örneklerini kör gönderme.
- [ ] 500 karakter sınırı olan alanları gerçek final metniyle say.
- [ ] ChatGPT e-postası, maintainer rolü ve gerekiyorsa Organization ID'yi kullanıcıdan güvenilir yolla doğrulat.
- [ ] Form ve koşulları son kez kullanıcıya göster; başvuru gönderimini ayrı onayla.
- [ ] Gönderim teyidini özel sakla; bekleme durumunu `SubmittedAwaitingDecision` yap.

**Kabul:** G7 ve ayrıca onaylı gönderim yapıldıysa gerçek teyit var. Kabul/aktivasyon yalnız dış sonuç oluşursa G8'e çıkar.

### W17 — İsteğe bağlı ChatGPT mağaza paketi

**Bağımlılık:** G3–G5, güncel platform uyumluluğu ve seçilen mimariye göre barındırma onayı.  
**Dosyalar:** `plugins/job-application-agent/`, `docs/guides/CHATGPT_PLUGIN.md`, `docs/grant/STORE_TRACK.md`.

- [ ] Bölüm 25 karar ağacına göre skills-only / MCP / karma paketi seç.
- [ ] Gerçek gereksinimi olmayan bulut servisi açma; local companion bağımlılığını gizleme.
- [ ] Resmî olmayan LinkedIn bağlantısını mağaza incelemesinde saklama veya sonradan açılacak gizli özellik olarak planlama.
- [ ] Güncel yayın şemasıyla test hesabı, metadata, izinler ve test senaryolarını hazırla.
- [ ] Kullanıcı onayından sonra submit; mağaza kabulünü Pro hibesi diye kaydetme.

**Kabul:** Bu ayrı ürün dağıtım işidir. Tamamlanmaması, Codex for OSS başvurusu için otomatik eksik şart anlamına gelmez.

### W18 — İsteğe bağlı tam masaüstü / fine-tuning genişlemesi

**Bağımlılık:** Bölüm 12.3 veya 13.4'teki gerekçe kapısı.  
**Dosyalar:** Ayrı ADR, ayrı plan, ayrı test raporu.

- [ ] Hangi ölçülmüş sorunu çözeceğini tanımla.
- [ ] Mevcut daha dar yöntemlerin niçin yetmediğini göster.
- [ ] Yetki, veri ve maliyet onaylarını al.
- [ ] İzole deney yap; baseline karşılaştırmasını raporla.
- [ ] Fayda yoksa ürüne ekleme.

**Kabul:** “Daha gelişmiş görünsün” gerekçesi kabul edilmez. Bu görevlerin yapılmaması projenin eksik veya başvuruya uygunsuz olduğu anlamına gelmez.

## 19. Çalıştırma, doğrulama ve kurulum sözleşmesi

Aşağıdaki script'ler **gelecek uygulayıcının oluşturacağı teslimlerdir**. Bu belge teslim edilirken mevcut bir yazılım oldukları iddia edilmiyor.

| Script | Davranış | Hata davranışı |
|---|---|---|
| `doctor.ps1` | SDK, sürüm, browser, özel veri güvenliği, port ve model kipini kontrol eder | Eksik bağımlılığı ve güvenli çözümü açıkça söyler |
| `bootstrap.ps1` | Sabitlenmiş paketleri restore eder, frontend kurar, gerekli browser binary kurulumunu yönlendirir | Gizli yazılım veya global izin değişikliği yapmaz |
| `run-demo.ps1` | Fixture kipinde sentetik uygulama ve test sitesini açar | Gerçek hedefe fallback yapmaz |
| `verify.ps1` | Format/build/test/sentetik E2E/secret taraması sonuçlarını toplar | Herhangi bir zorunlu kontrol başarısızsa nonzero çıkış |
| `package.ps1` | Seçili platform için release çıktılarını ve checksum üretir | Özel veri veya izin verilmeyen dosya varsa paketi reddeder |
| `export-public-evidence.ps1` | Yalnız onaylı, anonim ölçümleri public rapora çıkarır | Ham başvuru/CV alanı algılanırsa engeller |

### 19.1 Kullanıcı quickstart hedefi

Geliştirildikten sonra README'de gerçekten çalıştırılacak yol:

```powershell
# Gerçek repo yayınlandıktan sonra kullanılacak örnek akış.
git clone https://github.com/caglarhekimci/job-application-agent.git
cd job-application-agent
pwsh ./scripts/doctor.ps1
pwsh ./scripts/bootstrap.ps1
pwsh ./scripts/run-demo.ps1
```

`pwsh` yoksa sessiz kurma; resmî kurulum yolunu göster. Windows PowerShell uyumluluğu sağlanırsa ayrıca test et. Genel execution policy'yi sistem çapında gevşeten kalıcı komut önerme.

### 19.2 Geliştirici doğrulaması

```powershell
dotnet restore JobAgent.slnx
dotnet build JobAgent.slnx --configuration Release --no-restore
dotnet test JobAgent.slnx --configuration Release --no-build
pwsh ./scripts/verify.ps1
```

Proje/SDK test runner davranışı farklıysa script'i o gerçek ortama göre güncelle, README'deki komutları aynı anda düzelt. Başarılı görünmek için testleri `skip` veya `continue-on-error` ile gizleme.

### 19.3 Yerel MCP kurulumu

Codex'in resmî belgeleri STDIO ve HTTP MCP taşımasını, `codex mcp add` gibi yönetim komutlarını anlatıyor. Önce kurulu sürümde `codex mcp --help` kontrol edilir. [S09][s09]

Hedef kurulum biçimi:

```powershell
# Bu komutlar JobAgent.Mcp projesi uygulandıktan sonra çalıştırılacak.
dotnet publish ./src/JobAgent.Mcp/JobAgent.Mcp.csproj --configuration Release --output ./artifacts/mcp
$mcpDll = (Resolve-Path "./artifacts/mcp/JobAgent.Mcp.dll").Path
codex mcp add job-application-agent -- dotnet "$mcpDll"
codex mcp list
```

Kurulum ekranı aynı doğrulanmış DLL konumundan komut üretsin. `artifacts/` build dizini gitignore'a alınır. Kurulu Codex sürümünün komut biçimi farklıysa gerçek `--help` çıktısına göre kılavuz ve script birlikte düzeltilir.

STDIO yerel proses olduğu için hosted ChatGPT'nin doğrudan kullanacağı remote servisle aynı şey değildir. Yerel sunucuyu geçici public tünelle açmak, kimlik doğrulama ve veri güvenliği çözümü yerine geçmez.

### 19.4 Model seçimi

Kullanıcı Codex/ChatGPT arayüzünde erişebildiği modeli ve muhakeme düzeyini seçer. Bir prompt, model seçiciyi veya araç yetkilerini kendiliğinden değiştirmez. Uygulayıcı `GPT-6 Astra Çok Yüksek` ifadesini kör bir API model ID'sine dönüştürmez; bulunduğu ortamın gerçek yapılandırmasını kullanır.

## 20. CI, test raporları ve sürüm disiplini

### 20.1 CI aşamaları

1. Checkout ve sabitlenmiş araç zinciri.
2. Lockfile'a bağlı restore/kurulum.
3. Format/statik analiz ve derleme.
4. Unit ve integration testleri.
5. Gerçek browser ile yalnız sentetik E2E.
6. Secret ve bağımlılık/lisans kontrolleri.
7. Rapor ve izinli test artifact'lerinin kısa saklama süresiyle yayınlanması.

Public fork PR'larında gerçek hesap veya API secret'ı bulunmaz. Güvenilmeyen PR içeriğiyle yetkili workflow çalıştıran tehlikeli tetikleme düzenleri kurulmaz. Workflow izinleri en az ihtiyaçla sınırlandırılır; action sürümleri doğrulanmış sürüm/commit'e sabitlenir.

### 20.2 Test kapsamı

Satır kapsamı yardımcı metriktir; tek başarı ölçüsü değildir. Onay, alıcı, maaş, veri paylaşımı ve tekrar gönderim kuralları için davranış testleri zorunludur. İşlevi bulunmayan dosyalar ekleyerek kapsam veya commit sayısı artırılmaz.

Browser testlerinde retry bir flakiness göstergesi olarak raporlanır; tekrar deneyerek sonunda yeşile dönen sonucu ilk seferde başarılı gibi sunma. Kritik onay/gönderim testleri deterministik olmalı.

### 20.3 Release doğrulama kaydı

Her release için `docs/VERIFICATION.md` veya release artifact'inde şu alanlar olmalı:

```text
Commit / tag:
Date (UTC):
OS / runtime / browser versions:
Exact commands:
Exit codes:
Test cases passed / failed / skipped:
Fixture model or actual model:
Authorized live targets, if any:
Known limitations:
Artifact checksums:
Reviewer:
```

Bu başlıklar rapor şablonudur; release sırasında gerçek değerler yoksa alanı `NotRun`, `NotApplicable` veya `BlockedExternal` olarak açık yaz. Başarısız bir testin çıktısını silme; düzeltme sonrası yeni koşuyu ayrıca ekle.

## 21. Açık kaynak sunumu: Güçlü görünmek değil, kolay doğrulanmak

### 21.1 README sırası

İngilizce README ana, Türkçe README eş içerikte yardımcı olsun:

1. Kullanıcının sorununu ve çözümü iki paragrafta anlat.
2. Gerçek özellik durum tablosu koy.
3. Anahtarsız sentetik quickstart ver.
4. Kısa demo ve kişisel kullanım yolu göster.
5. Kapsamı ve platform izin sınırlarını açık yaz.
6. Mimari, test ve benchmark raporlarına bağlan.
7. Veri güvenliği, kurulum/katkı, lisans ve destek kanallarını göster.

Önerilen değer cümlesi:

> A local-first, evidence-grounded job application agent with reusable candidate memory, approval-bound browser actions, and reproducible synthetic form tests.

Bu cümle, yalnız tarif ettiği özellikler çalıştığında public README'nin mevcut ürün açıklaması olarak kullanılmalı. Öncesinde “under development” etiketi bulunmalı.

### 21.2 Yetenek matrisi örneği

| Özellik | İlk belgede durum | Kanıt oluşunca güncelleme |
|---|---|---|
| Kaynaklı aday hafızası | Planned | Test ve demo linki |
| Çok adımlı sentetik form gönderimi | Planned | E2E raporu |
| Yerel Codex MCP | Planned | Kurulum smoke kaydı |
| İzinli gerçek kariyer sitesi | Permission-dependent | Belirli hedef ve tarih |
| LinkedIn otomatik başvuru | PermissionRequired | Yetki ve canlı test olmadan değişmez |
| Hosted ChatGPT plugin | Optional / NotPublished | Mağaza sonucu oluşunca |

### 21.3 Topluluk ve bakım dosyaları

Lisans, katkı kılavuzu, davranış kuralları, issue/PR şablonları ve güvenlik bildirimi yolu bulunmalı. GitHub'ın topluluk sağlığı dosyaları bunun için yerleşik mekanizma sunuyor. [S18][s18]

`CONTRIBUTING.md`: Kurulum, test, kod standartları, AI ile üretilen katkıda insan sorumluluğu, hassas veri paylaşmama ve küçük PR beklentisi.

`SECURITY.md`: Desteklenen sürümler ve gerçek bildirim kanalı. CV/oturum verisini public issue'ya koymamaları açık yazılır. Kurulmayan bir özel kanal adresi uydurulmaz; repo güvenlik bildirim özelliği veya gerçek sahip iletişimi onayla yapılandırılır.

Issue şablonları: kurulum hatası, form uyumluluk hatası, yanlış cevap, özellik önerisi. Her şablonda sentetik tekrar üretim ve redaction uyarısı bulunur.

### 21.4 Public yayın öncesi kontrol

- [ ] Lisans hakları ve üçüncü taraf bildirimleri kontrol edildi.
- [ ] Tüm git geçmişi ve paketlerde secret/kişisel veri taraması yapıldı.
- [ ] README'de yalnız çalışan özellikler mevcut zamanda anlatılıyor.
- [ ] Sentetik demo açık etiketli; gerçek kişi ve kurum verisi görünmüyor.
- [ ] Gerçek kurulum, test, release ve destek bağlantıları açılıyor.
- [ ] Kullanıcı repo görünürlüğünü ve push kapsamını onayladı.
- [ ] Başvuru formundaki özel hesap bilgileri public klasörde yok.

Bu belge public repoya alınabilir; ancak gerçek pilot, başvuru ve hesap verileriyle doldurulan özel kopyalar otomatik public'e taşınmaz.

## 22. Gerçek benimsenme ve bakım planı

### 22.1 İlk hedef kitle

.NET geliştiricileri, MCP/ajan araçları geliştirenler ve kendi başvuru sürecini yerel yönetmek isteyen teknik kullanıcılar. İlk pilotun herkese hitap etmesi gerekmez. Yeniden kullanılabilir test ve hafıza katmanını başka geliştiricilerin kendi uygulamalarında deneyebilmesi ek faydadır.

### 22.2 İç pilot hedefleri — resmî kabul eşiği değildir

Örnek başlangıç hedefi: 5 bağımsız kişinin sentetik demoyu kurması; bunların en az birkaçından somut geri bildirim alınması; gerçek kurulum/ürün hatalarının düzeltilmesi; en az bir bakım release'i yayımlanması.

Bu sayılar **planlama hedefidir**. Beş kişinin kullanması kabul garantisi değildir; sayı elde edilmeden belgede gerçekleşmiş gibi yazılmaz. Yakın arkadaş testleri bağımsız topluluk benimsemesiyle aynı kategoriye zorla sokulmaz; nasıl toplandığı açıklanır.

### 22.3 Davet ve geri bildirim

Onaylı topluluklarda tek, ilgili ve dürüst bir paylaşım yap. “Altı ay Pro kazanmak için yıldız atın” yerine gerçek çözümü ve geri bildirim ihtiyacını anlat. Ücretsiz test etmeleri için API anahtarsız demo sun. Gerçek CV'yi geliştiriciye göndermeleri gerekmesin.

Sorular: Kurulumda nerede durdun? Hangi sonucu anlamadın? Hangi cevap yanlıştı? Onay ekranı güven verdi mi? Tekrar kullanır mısın, hangi koşulda? Hangi modülü kendi projenizde kullanırsın?

Geri bildirimleri kullanıcı izniyle özetle. Kişi adı veya alıntı yayınlamak için izin al; referans uydurma. Kullanıcıya baskıyla olumlu yorum yazdırma.

### 22.4 Bakım döngüsü

Yeni issue'yu yeniden üret → önem/scope etiketle → sebebi açıkla → failing test → fix → review → release notu → geri bildirim sahibine sonucu bildir. Bir PR'ı model yazsa bile bakımcı anlamadan birleştirmesin.

Gerçek bir sorun yoksa destek formunda “yüksek issue yüküm var” deme. Yeni projede bakım sorumluluğunu, bugün var olan test/bağımlılık/uyumluluk işleriyle anlat. Yüzeysel commit ve AI üretilmiş boş PR yığını gerçek bakım kanıtı değildir.

### 22.5 Kaçınılacak yöntemler

Yıldız satın alma, hesap botları, sahte indirme, tekrar indirmeleri kullanıcı diye sayma, var olmayan şirket logoları, kendi kendine yazılmış bağımsız kullanıcı yorumu, başka projelere görünürlük için alakasız PR gönderme, sponsor/ortaklık iddiası, kabul edilmemiş hibeyi “award” diye README'ye koyma.

Program desteği projenin sebebinin bir parçası olabilir; değer önerisini gerçekte yapmadığı işleri vaat ederek büyütme. Açık kaynak itibarını kısa süreli destek için riske atma.

## 23. Kanıt dosyası ve ölçüm metodolojisi

### 23.1 Public kanıt dizini

`docs/evidence/` altında yalnız anonim, tekrar üretilebilir, izinli kanıtlar:

- `QUALITY.md`: Test tanımı, veri sürümü, koşu komutu ve gerçek sonuçlar.
- `ADOPTION.md`: Tarihli gerçek kurulum/geri bildirim sayıları ve toplama yöntemi.
- `MAINTENANCE.md`: Gerçek issue, fix PR ve release bağlantıları.
- `METRICS.md`: Metrik tanımları, sınırlamalar ve veri kaynağı.
- `REUSABILITY.md`: Başka projede kullanılmışsa gerçek örnek; değilse “henüz dış kullanım kanıtlanmadı”.

Kişisel başvuru ayrıntıları ve özel e-postalar burada tutulmaz. Ekran görüntülerinde bulanıklaştırmanın yeterliliği gözle kontrol edilir; mümkünse baştan sentetik veriyle çekilir.

### 23.2 Ölçüm kayıt biçimi

```text
metric_name, period_start, period_end, value, unit, source, definition, exclusions, verified_at
```

Örnek metrik kategorileri: tekil bağımsız kurulum teyidi; tekrar kullanım teyidi; gerçek bildirilen hata; kapatılan yeniden üretilebilir hata; testli fix; release sayısı; desteklenen doğrulanmış akış sayısı.

GitHub stars “star sayısı”, downloads “indirme” olarak yazılır. Bunları otomatik aktif kullanıcı sayısına çevirme. CI bot indirmeleri, geliştiricinin kendi koşuları ve sentetik oturumlar gerçek kullanıcı metriğinden ayrılır.

### 23.3 Zaman kazancı ölçümü

Aynı/karşılaştırılabilir sentetik görevde manuel hazırlık ve ajan destekli hazırlığı ölç. Kurulum süresini saklama; bir kerelik kurulum ile tekrar başvuru süresini ayrı raporla. Kullanıcı inceleme/düzeltme süresini ajan süresinden çıkarıp iyileşmeyi şişirme.

Ölçüm küçük bir pilot ise örneklem sayısı ve belirsizliği belirtilir. “İşe girme olasılığını artırır” veya “ATS'yi geçer” gibi bu deneyin göstermediği sonuçlar çıkarılmaz.

### 23.4 Başvuru anlatısını kanıta bağlama

Her iddiayı bir kaynakla eşleştir:

| İddia | Uygun kanıt |
|---|---|
| “Aktif bakımcıyım” | Gerçek inceleme/düzeltme/sürüm sorumluluğu |
| “Başkaları kullanıyor” | Kullanıcının izniyle doğrulanan bağımsız kullanım |
| “Yeniden kullanılabilir” | Belgeli API/örnek; dış kullanım varsa gerçek bağlantı |
| “Güvenliği önemsiyor” | Tehdit modeli + çalışan negatif test + kapatılmış bulgu |
| “Codex iş yükümü azaltır” | Somut backlog ve ölçülebilir bakım görevi |

Kodun büyük olması, kullanılan modelin adı veya README görselliği bu kanıtların yerine geçmez.

## 24. Codex for Open Source başvurusu: A'dan Z'ye

### 24.1 Başvurmadan önce

- [ ] Programın ve koşulların güncel sürümünü oku; kaynak doğrulama tarihini yaz.
- [ ] Profil ve başvuruda verilecek repo gerçekten public mi doğrula.
- [ ] Gerçek primary/core maintainer rolünü ve repo kontrolünü doğrula.
- [ ] Geçerli ChatGPT hesabını ve varsa güncel ülke/yerel uygunluk kısıtlarını doğrula; başvuru e-postasını kullanıcıdan al, GitHub e-postasıyla aynı sayma.
- [ ] Repo açıklamasında kullanım/önem ve bakım iddialarını kanıta bağla.
- [ ] İsteniyorsa Codex Security/API kredi gereksinimini ayrı değerlendir.
- [ ] Başvuru ekranındaki güncel zorunlu alanları kontrol et; bu belgeyi değişmez form şeması kabul etme.

### 24.2 Form alanları

18 Eylül 2026'da erişilen form şu alanları gösteriyor: ad, soyad, ChatGPT hesabının e-postası, public GitHub kullanıcı adı ve repo URL'si, bakımcı rolü, projenin neden uygun olduğu, Security/API kredi ilgisi, Organization ID, kredi kullanım açıklaması ve ek not. Uygunluk, kredi kullanımı ve ek not alanları 500 karakterle sınırlı. Organization ID/kredi alanlarının canlı ekranda hangi seçimle zorunlu olduğunu gönderim sırasında kontrol et. [S01][s01]

| Alan | Kullanılacak veri / karar |
|---|---|
| Ad/soyad | Kullanıcının gerçek beyanı; bu projede Çağlar Hekimci olarak kullanıcıya doğrulatılır |
| E-posta | ChatGPT hesabındaki gerçek e-posta; public dokümana yazılmaz |
| GitHub | `caglarhekimci` |
| Repo | Yalnız gerçekten yayınlandıktan sonra doğrulanan URL |
| Rol | Yapılan gerçek işe göre primary veya core maintainer |
| Uygunluk anlatısı | Var olan kullanım veya somut ekosistem değerini kısa anlat |
| Security ilgisi | Gerçek repo güvenlik ihtiyacı varsa seç; erişim garantisi sayma |
| API kredi ilgisi | Gerçek bakım/eval otomasyon planı varsa seç |
| Organization ID | İsteniyorsa kullanıcının doğru OpenAI organizasyonu; uydurma kimlik yok |
| Ek not | Önemli sınırlama, yeni proje durumu veya doğrulanabilir bakım planı |

### 24.3 İngilizce yanıt taslakları

**Önemli:** Aşağıdaki metinler bugün mevcut bir ürünün kanıtı değildir. Tarif ettikleri özellikler ve bakım rolü gerçekleştikten sonra kullanılabilecek kısa taslaklardır. Son tarihteki gerçek proje durumu neyse ona göre düzenlenir; veri yoksa sayı eklenmez.

**A — “Why does this repository qualify?” / yeni, henüz geniş benimsenmemiş proje için dürüst taslak:**

> I maintain an early-stage, local-first job application agent with evidence-backed answers, approval-bound browser actions, and reproducible synthetic form tests. Its candidate-memory and evaluation components are designed for reuse by other agent developers. Adoption is still developing; the repository documents tested capabilities, limitations, and maintenance work.

**B — API kredisi talep edilecekse bakım odaklı taslak:**

> I would use API credits for reproducible regression evaluations on synthetic data, review assistance, and maintaining MCP and browser adapters. Runs would be budget-capped and versioned. Candidate CVs, credentials, and private application data would not be included in public datasets or CI logs.

**C — Ek not taslağı:**

> I am the primary maintainer and will use Codex for test expansion, dependency updates, issue reproduction, code review, and release work. Platform permissions and live-integration limits are documented explicitly. I do not claim authorized LinkedIn automation where that permission has not been obtained.

Kullanım gerçekten oluştuğunda A metninin bir kısmını doğrulanmış tarihli sayı veya kısa gerçek reuse örneğiyle değiştir. “Adoption is still developing” ifadesini geniş benimsenme varmış gibi çevirmek yerine mevcut durumu koru.

Karakter sayısını gönderilecek son metin üzerinde kontrol etmek için:

```python
from pathlib import Path

# Bu dosya özel başvuru alanında, kullanıcının son onayladığı metni içermelidir.
text = Path("answer.txt").read_text(encoding="utf-8").strip()
count = len(text)
print(f"Characters: {count}/500")
if not text or count > 500:
    raise SystemExit("Answer is empty or exceeds the 500-character limit.")
```

Formun kendi sayacı esas alınır. Linkleri ve noktalama işaretlerini de karakter bütçesine dahil et. Birden fazla alanı birleştirip tek 500 sınırı varmış gibi değerlendirme.

### 24.4 Form gönderimi

Uygulayıcı yapay zekâ formu güncel sayfadan okuyabilir ve taslağı hazırlayabilir; fakat kullanıcı adına program şartlarını kabul ederek başvuruyu göndermeden önce final alanları ve hesap hedefini kullanıcıya göstermelidir. “Bu projeyi geliştir” onayı, hibe formunu gizlice gönderme yetkisi değildir.

Gönderim sonrası görünen teyidi ve tarihi özel alana kaydet. E-posta geldiğini görmeden “başvuru kabul edildi” deme. Aynı fayda için farklı hesaplarla tekrar başvurma; gerekirse yalnız resmî süreçle doğru bilgi güncellemesi yap.

### 24.5 Değerlendirme süreci

Gerçek yeni sürümler ve kullanıcı sorunları üzerinde çalışmaya devam et. E-posta beklentisine sabit bir gün/saat atama. Gönderimden sonra bakım bırakılmaz; proje gerçek ihtiyacı karşılamaya devam etmeli.

Kabul edilmezse veya cevap gelmezse bunu mühendislik başarısızlığıyla eşitleme. Varsa verilen gerekçeyi incele; eksik kullanım/önem kanıtını gerçek faaliyetle geliştir. Yeniden başvuru politikasını resmî kaynaktan doğrulamadan düzenli spam başvurusu oluşturma.

## 25. ChatGPT'ye yükleme ve mağaza başvurusu — ayrı yol

### 25.1 İki farklı başvuru

**Codex for OSS:** Bakımcı desteği için başvuru.  
**ChatGPT plugin mağazası:** Ürünün dağıtımı ve incelemesi.

Birinin kabul edilmesi diğerinin kabulünü veya üyelik ödülünü otomatik sağlamaz. Mağaza yolunu ancak daha çok kullanıcının ürüne gerçekten ulaşmasına yardım edecekse yürüt.

### 25.2 Paket seçimi

**Yerel Codex:** Önce STDIO MCP; kullanıcı kendi bilgisayarında çalıştırır. Kurulum ve destek sınırları açık anlatılır.

**Skills-only paket:** Rehber ve yeniden kullanılabilir görev davranışı dağıtılabilir. Ancak kullanıcıya yerel companion olmadan sahip olmadığı backend/browser yeteneği varmış gibi sunulmaz.

**Remote MCP / karma paket:** Hosted erişim gerçekten gerekiyorsa seçilir. Kimlik doğrulama, kullanıcı ayrımı, güvenli depolama ve kararlı servis gerekir. Yerel bilgisayara erişim kendiliğinden oluşmaz.

### 25.3 Güncel mağaza şartları

Resmî yayın akışı doğrulanmış geliştirici kimliği, paket/servis bilgileri ve inceleme materyalleri ister. Remote MCP için üretime uygun erişim ve gerektiğinde demo hesap; arayüz varsa ilgili güvenlik yapılandırmaları hazırlanır. İnceleme için en az 5 olumlu ve 3 olumsuz senaryo istenir. Onay sonrası yayınlama ayrı adımdır. [S08][s08]

Üçüncü taraf izinlerini aşan veya resmî olmayan bağlantıyı asıl hizmet olarak sunan ürünler platform kurallarına takılabilir. Bu yüzden LinkedIn otomasyonunun yetki durumunu saklamak bir yayın stratejisi değildir. [S07][s07]

### 25.4 Bizim inceleme senaryolarımız

**Olumlu:** Sentetik CV inceleme; doğru aylık maaş cevabı; şirket kapsamlı hafıza; izinli sandbox formunun doldurulması; kullanıcı onayı sonrası receipt.

**Olumsuz:** Olmayan deneyim yazdırma; modelin kullanıcı onayı üretmesi; yetkisiz LinkedIn otomasyonu açtırma.

Ek senaryolar: yanlış net/brüt, eski onay, alıcı değişimi ve private minimum sızıntısı. İnceleme hesabında gerçek CV, gerçek işveren hesabı veya kişisel MFA bilgisi bulunmaz.

### 25.5 Yayın kontrol listesi

- [ ] Güncel SDK/paket manifest şeması kullanıldı.
- [ ] Ad, açıklama ve özellikler dürüst; resmî ortaklık çağrışımı yok.
- [ ] Gizlilik, kullanım koşulları ve destek kanalı gerçek.
- [ ] Tool izinleri/yazma notları doğru.
- [ ] Gerekli kimlik/alan adı doğrulaması kullanıcı tarafından yapıldı.
- [ ] İnceleme hesabı ve sentetik veriler hazır.
- [ ] Local companion bağımlılıkları açık.
- [ ] Kullanıcı başvuruyu ve gerekiyorsa barındırma maliyetini onayladı.
- [ ] Submit, review, approved ve published durumları ayrı kaydediliyor.

## 26. Kabul sonrası ve altı aylık dönemin yönetimi

### 26.1 Destek e-postası geldiğinde

Göndereni ve resmî alan adlarını doğrula; şüpheli bağlantılara kimlik bilgisi girme. E-postanın hangi hesabı, faydayı, süreyi ve varsa son kullanım/aktivasyon tarihini belirttiğini oku. Kampanya kodu veya kişisel davet varsa public repoya koyma.

Mevcut ücretli aboneliğin üstüne nasıl uygulanacağını varsayma. Aynı ChatGPT hesabında etkinleştiğini kontrol et; çakışma varsa resmî destek kanalından netleştir. Mevcut üyeliği sırf plan gereği erken iptal etme; stacking, otomatik yenileme veya nakit karşılık iddiası kurma. Program koşulları faydaların kişisel ve devredilemez olduğunu, kapsamın program tarafından belirlendiğini açıklıyor. [S03][s03]

### 26.2 Codex'i gerçekten hangi işlerde kullanacağız?

Test üretmek ve mevcut testleri anlamak; hata yeniden üretmek; bağımlılık güncellemelerinin etkisini incelemek; browser/MCP değişikliklerini uyarlamak; güvenlik bulgularını doğrulamak; PR review; sürüm notları; benchmark regresyon analizi.

Başka projelerin kodunu/özel kullanıcı verisini yetkisiz tarama yok. Codex Security erişimi varsa yalnız yetkili repolarda kullanılır. API kredisi verilmişse belirtilen kapsam ve bütçe takibi uygulanır; sınırsız genel tüketim hakkı gibi görülmez.

### 26.3 Altı ay bitince

Kullanıcı yerel verisine ve açık kaynak koda erişmeye devam edebilmeli. Model sağlayıcısı değiştirilebilir, Fixture kip çalışır, özel profil dışa aktarılabilir. Ürün, yalnız geçici Pro faydası var olduğu sürece açılabilen kapalı bir demoya dönüşmez.

Yenileme veya ikinci destek garantisi verilmez. Maintenance kapasitesi azaldıysa README'de dürüstçe açıklanır; kritik güvenlik sorunları görünmez bırakılmaz. Gerektiğinde sürdürülebilir biçimde kapsam daraltılır veya bakım devri değerlendirilir.

## 27. Riskler, maliyetler ve haricî işler

### 27.1 Risk kaydı

| Risk | Önlem / doğru tepki |
|---|---|
| Program değişir veya proje seçilmez | Güncel koşulu tekrar oku; kişisel faydayı bağımsız sürdür; destek garantisi verme |
| LinkedIn izin vermez | Canlı adaptör kapalı; kullanıcı ilan aktarımı ve diğer izinli kaynaklar; sınırı açık yaz |
| Model yanlış kişisel cevap üretir | Kanıt/şema/kurallar, insan incelemesi, regresyon testleri |
| Yanlış maaş paylaşılır | Tipli maaş ve kapsam, private minimum ayrımı, veri paylaşım izni |
| CV/oturum public olur | Repo dışı store, secret scan, paket inceleme, iptal/rotate süreci |
| Başvuru iki kere gider | Tek aktif işlem, onay/idempotency, belirsiz sonuçta tekrar etmeme |
| Tarayıcı değişir | Adaptör izolasyonu, fixture senaryoları, sürüm testleri |
| Projesi büyük ama kimse kullanmaz | Küçük çalışan dilim, temiz kurulum, hedefli gerçek pilot |
| Maliyet yükselir | Fixture/host-mediated başlangıç, bütçe kapısı, model/eylem sınırı |
| Mağaza reddi | Ayrı dağıtım kanalı; şartları gerçekten sağla, gizleme veya bypass yok |
| Agent bütün işi tamamlandı sayar | Ayrı mühendislik/canlı izin/topluluk/program durumları |

### 27.2 Maliyet politikası

İlk hedef ücretli API, domain veya sunucu almadan yerel demo ve mevcut araçlarla geliştirmedir. Paket indirmek ücretsiz olsa bile kullanılabilir ağ/hesap kota sınırları doğrulanır. Gerçek model/API ve bulut açılışı için tutarı belli bütçe onayı gerekir.

Ücretsiz Pro elde etmek amacıyla kontrolsüz harcama yapılmaz. Her yeni maliyetin kişisel fayda veya gerçek OSS bakım yararı açıklanır. Programdan API kredisi çıkmadığında da proje sürdürülebilir bir asgari kipte çalışmalıdır.

### 27.3 Yapay zekânın tamamlayabilecekleri

Kod, test, sentetik veri, doküman, kurulum script'i, build paketleri, model değerlendirme runner'ı, kanıt toplama şeması, public tanıtım taslağı ve form yanıt taslağı. Araçları ve izinleri varsa bunları gerçekten çalıştırıp doğrulayabilir.

### 27.4 Yapay zekânın gerçekleşmiş sayamayacakları

Kullanıcının gerçek CV/maaş onayı; hesap sahipliği/kimlik doğrulama; platformun izin vermesi; ücretli harcama kararı; bağımsız insanların gerçekten kullanması; topluluğun olumlu geri bildirim vermesi; mağaza veya Codex for OSS kabul kararı.

Bu işler için kullanıcı/üçüncü taraf katılımı gerekir. Uygulayıcı geri kalan yerel işleri yapmayı bırakmamalı; yalnız ilgili haricî kapıyı açıkça beklemede tutmalıdır.

## 28. Son teslim kontrolü ve sonraki AI'a çalışma talimatı

### 28.1 Mühendislik teslimi

- [ ] Çalışan kaynak kod ve temiz kurulum.
- [ ] Aday profili, maaş ve kapsamlı hafıza.
- [ ] Gerekçeli ilan değerlendirmesi ve doğru cevap motoru.
- [ ] Gerçek tarayıcıdaki sentetik tam başvuru; doğru CV yükleme.
- [ ] Kullanıcı kontrollü veri paylaşımı ve gönderim onayı.
- [ ] Belirsiz sonuç/tekrar gönderim kontrolü.
- [ ] Yerel MCP; doğrulanmış host örneği veya dürüst eksik yetenek kaydı.
- [ ] Kritik güvenlik ve regresyon testleri.
- [ ] API anahtarsız demo ve gerçek raporlar.
- [ ] Özel verisiz repo, lisans ve yayınlanabilir paket.

### 28.2 Açık kaynak / program teslimi

- [ ] Gerçek public repo ve sürüm; yayın onayı alınmış.
- [ ] Bağımsız kurulum ve geri bildirim durumu dürüstçe belgelenmiş.
- [ ] Gerçek bakım işleri ve kanıt bağlantıları.
- [ ] Güncel program alanlarına göre 500 karakterlik doğru taslaklar.
- [ ] Kullanıcının final onayı yoksa form gönderilmemiş.
- [ ] Kabul ve aktivasyon dış karar olarak ayrı tutulmuş.
- [ ] LinkedIn canlı özelliğinin izin/test durumu açık.

### 28.3 Uygulayıcının ilk eylemleri

1. Bu belge, yürütme prompt'u ve mevcut repo/çalışma ağacını oku.
2. Güncel kaynakları ve gerçek araç yeteneklerini kontrol et.
3. W00 durumunu oluştur; küçük alt planlara böl.
4. W01'den başlayarak gerçek failing test → uygulama → doğrulama döngüsünü yürüt.
5. İlk çalışan sentetik dikey dilimi mümkün olan en erken aşamada göster.
6. Sonra güvenilirlik, kurulum ve gerçek kullanım kapılarını tamamla.
7. Haricî işler bekliyor diye yerel geliştirmeyi durdurma; eksikliği gizleyerek de “A'dan Z'ye tamamlandı” deme.

### 28.4 Oturum sonunda rapor formatı

**Tamamlanan:** Dosyalar, özellikler ve kanıtları.  
**Çalıştırılan:** Gerçek komutlar ve sonuçlar.  
**Eksik/engelli:** Sebep, gereken kullanıcı/haricî eylem, kalan risk.  
**Sonraki görev:** Tek bir belirli iş paketi ve mevcut dosya yolları.  
**Dış etkiler:** Repo push, form gönderimi, ücretli çağrı veya gerçek başvuru yapıldıysa kimin hangi onayıyla yapıldığı; yapılmadıysa açık ifade.

Bu projenin nihai başarı cümlesi “AI büyük bir repo yazdı” değildir: **Kullanıcı doğru bilgilerle başvuru yapabiliyor; başka geliştiriciler ürünü kurup doğrulayabiliyor; bakımcı gerçek bakım yapıyor; destek başvurusu bu gerçeği doğru anlatıyor.**

## 29. Kaynaklar ve yeniden doğrulama kaydı

Aşağıdaki resmî kaynaklar 18 Eylül 2026 araştırmasının dayanağıdır. URL yönlendirmesi, ürün adı, koşul ve arayüz değişebilir. Uygulayıcı özellikle program başvurusu ve canlı entegrasyon öncesi yeniden okumalıdır. Bu listedeki tasarım bağlantıları, geliştirme kararlarını gerekçelendirir; OpenAI'nin yayımlamadığı bir kabul puanlaması oluşturmaz.

| Kaynak | Konu | Uygulamadan önce kontrol |
|---|---|---|
| S01 | Codex for OSS başvuru formu | Program açık mı, alanlar ve karakter sınırı aynı mı? |
| S02 | Program yaklaşımı | Bakımcı/ekosistem ölçütleri değişmiş mi? |
| S03 | Program şartları | Uygunluk, kimlik, fayda ve aktivasyon koşulları |
| S04–S05 | LinkedIn kuralları | Tasarlanan eyleme izin var mı? |
| S06 | Computer-use güvenlik/entegrasyon | Onay ve veri aktarımı şartları |
| S07–S08 | Plugin kuralları ve yayın | Paket, veri, üçüncü taraf izinleri, inceleme |
| S09–S10 | MCP ve browser kurulumu | Gerçek host/işletim sistemi desteği |
| S11 | Faturalandırma | Abonelik ve API ayrımı |
| S12 | Evals | Ölçüm ve değerlendirme ilkeleri |
| S13–S15A | .NET, MCP SDK, Playwright | Desteklenen sürüm, paket, auth güvenliği |
| S16 | Greenhouse API | Okuma/gönderim ve izin koşulları |
| S17–S18 | GitHub OSS/topluluk | Lisans ve katkı mekanizmaları |
| S19–S20 | KVKK | Veri işleme ve dış aktarım değerlendirmesi |

[s01]: https://openai.com/form/codex-for-oss/ "OpenAI — Codex for Open Source application"
[s02]: https://developers.openai.com/community/codex-for-oss "OpenAI Developers — Codex for OSS"
[s03]: https://learn.chatgpt.com/docs/codex-for-oss-terms "Codex for Open Source Program Terms"
[s04]: https://www.linkedin.com/help/linkedin/answer/a1341387 "LinkedIn — Prohibited software and extensions"
[s05]: https://www.linkedin.com/legal/user-agreement "LinkedIn User Agreement"
[s06]: https://developers.openai.com/api/docs/guides/tools-computer-use-integration "OpenAI — Computer use integration"
[s07]: https://developers.openai.com/plugins/app-guidelines "OpenAI — Plugin guidelines"
[s08]: https://developers.openai.com/plugins/deploy/submission "OpenAI — Plugin submission"
[s09]: https://learn.chatgpt.com/docs/extend/mcp?surface=cli "ChatGPT Learn — MCP"
[s10]: https://learn.chatgpt.com/docs/chrome-extension "ChatGPT Learn — Browser extension"
[s11]: https://help.openai.com/en/articles/9039756 "OpenAI — ChatGPT and API billing"
[s12]: https://developers.openai.com/api/docs/guides/evaluation-best-practices "OpenAI — Evaluation best practices"
[s13]: https://dotnet.microsoft.com/en-us/platform/support/policy "Microsoft — .NET support policy"
[s14]: https://csharp.sdk.modelcontextprotocol.io/ "Official MCP C# SDK"
[s15]: https://playwright.dev/dotnet/docs/library "Playwright for .NET — Library"
[s15a]: https://playwright.dev/dotnet/docs/auth "Playwright for .NET — Authentication"
[s16]: https://docs.greenhouse.io/job-board.html "Greenhouse — Job Board API"
[s17]: https://docs.github.com/en/enterprise-cloud%40latest/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository "GitHub — Licensing a repository"
[s18]: https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file "GitHub — Community health files"
[s19]: https://www.kvkk.gov.tr/Icerik/2050/Kisisel-Veriler "KVKK — Kişisel veriler"
[s20]: https://www.kvkk.gov.tr/Icerik/2053/Yurtdisina-Aktarim "KVKK — Yurt dışına aktarım"

---

**Belgenin sonu.** Bu plan geliştirme ve dürüst program adaylığı içindir. Henüz gerçekleşmeyen kullanıcı benimsemesi, canlı LinkedIn yetkisi veya Pro kazanımı tamamlanmış sonuç olarak sunulamaz.

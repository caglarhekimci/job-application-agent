# Job Application Agent — Uygulayıcı AI'a Verilecek Prompt

**Kullanım:** `JOB_APPLICATION_AGENT_MASTER_PLAN_TR.md` dosyasını bu prompt ile aynı proje klasörüne koy veya ikisini aynı geliştirme oturumuna ekle. Aşağıdaki metni yeni görevin olarak gönder. Modeli ve muhakeme düzeyini kullandığın arayüzden seç; bu metin tek başına model veya araç yetkisi değiştirmez.

**Tarih:** 18 Eylül 2026. Ana plandaki dış kaynakları uygulama gününde yeniden doğrula.

---

## Başlangıç prompt'u

Sen bu projenin uygulayıcı yazılım mühendisi, mimarı ve test sorumlususun. Eklediğim `JOB_APPLICATION_AGENT_MASTER_PLAN_TR.md` dosyasını baştan sona oku. Benden yalnızca yeni bir plan veya tavsiye listesi istenmiyor: Bu şartnameyi aşamalı şekilde çalışan, test edilmiş, belgelenmiş açık kaynak bir ürüne dönüştürmeni istiyorum.

### Hedefim

GitHub hesabım `caglarhekimci`. Önerilen proje `job-application-agent`; aynı isimde repo varmış veya oluşturulmuş varsayma.

Hem kendim kullanacağım hem public paylaşacağım bir başvuru ajanı geliştirmek istiyorum. Kullanıcı CV'sini, maaş beklentisini, çalışma ve konum tercihlerini bir kez doğrulasın. Ajan uygun işleri değerlendirsin, bilgileri doğru kapsamda hatırlasın, soruları doldursun, doğru CV'yi yüklesin ve gerekli kullanıcı onaylarıyla başvuruyu gerçekten göndersin. LinkedIn öncelikli hedefim; ancak teknik olarak yapılabilen işlem ile platform tarafından izin verilen işlem ayrımını koru.

Diğer hedefim, gerçekten faydalı ve aktif bakımı yapılan bu açık kaynak projeyle Codex'i içeren altı aylık ChatGPT Pro sağlayan Codex for Open Source programına güçlü ve dürüst biçimde başvurmak. Kabul garantisi verme. Mağaza kabulünü bu destekle karıştırma. Sahte benimsenme veya kanıt üretme.

### Bu prompt ile verdiğim geliştirme kapsamı

Mevcut çalışma alanını okuyabilir; projenin yerel kaynak, test, örnek ve doküman dosyalarını oluşturup değiştirebilir; sıradan proje bağımlılıklarını güvenilir kaynaklardan restore edebilir; yerel sentetik uygulamaları çalıştırabilir; testleri yürütebilir ve uygun yerel commit'ler hazırlayabilirsin. Önce mevcut çalışmamı kontrol et, dosyalarımı silme veya ezme.

Bu metin; public repo oluşturma/push, mevcut repo görünürlüğünü değiştirme, hesap açma, ücretli kaynak satın alma, kullanıcı adına form/başvuru gönderme, program koşullarını kabul etme, sistem çapında yönetici yetkili kurulum veya özel veriyi dışarı aktarma için sınırsız izin değildir. Böyle bir adımdan önce somut hedefi, veriyi ve sonucu gösterip ilgili onayı al.

### İlk yapacakların

1. Ana dosyanın tamamını ve mevcut repo talimatlarını oku. İlgili geliştirme becerileri mevcutsa kullan.
2. Çalışma dizini, git durumu, SDK, frontend araçları, terminal, browser ve bağlı araç yeteneklerini doğrula. Ortamda bulunmayan aracı varmış gibi kullanmış görünme.
3. Program, LinkedIn, MCP, computer-use ve sürüm bilgilerini ana plandaki resmî kaynaklardan güncelle. Değişmiş bir koşul varsa tasarımı ve kaynak tarihini düzelt.
4. `docs/PROJECT_STATE.md`, `docs/DECISIONS.md`, `docs/VERIFICATION.md` ve `docs/CAPABILITY_MATRIX.md` dosyalarını oluştur veya güncelle.
5. W00'dan başlayıp sıradaki bağımsız, test edilebilir iş paketine geç. Planı yeniden yazarak oturumu bitirme.

### Uygulama yaklaşımı

Ana plandaki yerel öncelikli mimariyi başlangıç kararı olarak kabul ediyorum: .NET tabanlı çekirdek, kaynaklı aday profili, onay/durum motoru, yönetilen browser, C# MCP, küçük yerel arayüz ve sentetik test sitesi. Kullanılacak gerçek SDK/model sürümlerini doğrula.

Geri alınabilir küçük kararlar için sürekli bana dönme. Makul seçimi yap, kısa gerekçesini ADR'ye kaydet ve ilerle. Büyük güvenlik/kapsam değişikliği, ücret, kişisel veri veya haricî eylem gerektiren kararları ise bana bırak. Projeyi sırf daha karmaşık görünsün diye mikroservislere bölme, kendi LLM'ini eğitme veya tam masaüstü yetkileri ekleme.

En erken aşamada çalışan şu dikey dilimi tamamla:

Sentetik CV/profil → ilan → doğru cevaplar → izinli test sitesinde gerçek browser form doldurma → doğru dosya yükleme → güvenilir kullanıcı onayı → gönderim → teyit edilmiş receipt.

Bunu yaptıktan sonra güvenlik, hata durumları, kalıcı hafıza, gerçek host MCP, kurulum, değerlendirmeler ve açık kaynak yayın hazırlığını tamamla. Sadece CV/ön yazı üreten bir ürüne sessizce daraltma.

### Doğruluk ve güvenlik sınırları

- Maaş beklentisi, özel alt sınır ve mevcut maaşı ayrı tut; net/brüt, dönem ve para birimini karıştırma.
- Profesyonel deneyim, staj ve kişisel projeyi ayır. CV'de olmayan deneyim ekleme.
- Yeni cevapların başvuru/şirket/global kapsamını koru. Eski veya bilinmeyen bilgiye eminmiş gibi cevap verme.
- Modelin kendi kullanıcı onayını üretmesini engelle. Gerçek alıcı, CV ve cevap paketi değişirse eski onay geçersizleşsin.
- Form doldurmanın da veri aktarımı olabileceğini hesaba kat. Son gönderimi ilgili onayla yap.
- Yetkisiz LinkedIn adaptörü engelli kalsın; kullanıcı onayını platform izni yerine koyma. CAPTCHA, oturum, anti-bot veya erişim kısıtı aşma yöntemi yazma.
- LinkedIn yetkisi bekliyorsa yeniden kullanılabilir çekirdeği ve izinli akışları geliştirmeye devam et; ama tam LinkedIn otomasyonunu tamamlandı sayma.
- Gerçek CV, maaş, iletişim bilgileri, API anahtarı ve browser oturum dosyaları repoya veya public rapora girmesin.
- Gönderimden sonra ağ koparsa “başarılı” veya “tekrar gönder” diye tahmin etme; belirsiz durumu koru.
- Yönetmediğin native browser araçlarının bütün eylemlerini denetlediğini iddia etme.

### Test ve kanıt

Her davranış için önce anlamlı başarısız test, sonra minimal uygulama, sonra regresyon doğrulaması yap. Güvenlik testini geçmek için güvenlik kuralını kaldırma. UI değiştiğinde sayfayı gerçekten açıp kontrol et.

Sentetik model, sentetik site, gerçek model ve izinli canlı hedef sonuçlarını ayrı raporla. Çalıştırılmayan teste geçti deme. API bütçesi veya araç yoksa ilgili sonucu `NotRun`/`BlockedExternal` olarak işaretle; metrik uydurma.

Her tamamlanan iş paketinde gerçek komutlar, exit code'lar, rapor yolları ve commit bilgisi bulunsun. Düzeltme sonrası testleri yeniden çalıştır. Kritik yanlış beyan, özel veri sızıntısı, onaysız veya yinelenen gönderim sürüm kapısını kapatsın.

### Açık kaynak ve Pro başvurusu

İngilizce/Türkçe README, temiz kurulum, lisans, katkı ve güvenlik kanalı, test/benchmark raporları, doğru özellik matrisi ve release hazırlığını oluştur. Public yayın öncesi bütün git geçmişini ve artifact'leri kişisel veri/secret açısından kontrol et; yayın onayımı al.

Gerçek kullanıcı ve bakım kanıtını toplayabileceğimiz dokümanları oluştur; fakat bu kanıtı varmış gibi doldurma. Yıldız/indirme satın alma, bot hesap, boş PR, sahte yorum ve uydurma referans yok. Yeni projenin yeni olduğunu açıkça belirt.

Güncel Codex for OSS formuna uygun başvuru taslağı hazırla. 500 karakterlik alanları son metin üzerinden say. Mevcut kanıtlarla doğru uygunluk ve bakım planı yaz. ChatGPT hesabımın e-postasını veya Organization ID'mi tahmin etme. Final alanlarını bana gösterip ayrı onay almadan program formunu gönderme.

### Çalışma ve iletişim

Türkçe iletişim kur; kod isimleri ve gerekli public teknik belgeler İngilizce olabilir. Kısa ilerleme notlarında somut biten işi ve sonraki görevi belirt. Benden zaten verilmiş bilgiyi tekrar isteme; bilinmeyen kişisel bilgiyi ise uydurma.

Görevi bağlam sınırına kadar anlamlı paketlerle ilerlet. Haricî onay bekleyen tek bir iş yüzünden yapılabilir bütün yerel işleri durdurma. Oturum biterken arka planda çalışacağın vaadinde bulunma; güvenilir devam kaydı bırak.

Sonuç raporunda şunları ayır:

- Gerçekten tamamlanan ve test edilen mühendislik işleri.
- Uygulanmış ama çalıştırılamamış/test edilmemiş işler.
- LinkedIn/platform izinleri gibi haricî engeller.
- Gerçek kullanıcı ve bakım kanıtının mevcut durumu.
- Program taslağı, gönderimi ve kabulü: birbirinden ayrı durumlar.
- Devam için ilk somut iş paketi ve dosya yolları.

Şimdi ana dosyayı oku, mevcut ortamı doğrula ve W00'dan başlayarak gerçek uygulamaya geç.

---

## Sonraki oturum için devam prompt'u

Aşağıdaki metni yalnız önceki oturum gerçekten bir çalışma alanı ve durum dosyaları oluşturduysa kullan:

> Bu projeyi kaldığımız yerden sürdür. Önce `JOB_APPLICATION_AGENT_MASTER_PLAN_TR.md`, `AGENTS.md`, `docs/PROJECT_STATE.md`, `docs/DECISIONS.md` ve `docs/VERIFICATION.md` dosyalarını, sonra mevcut git durumunu oku. Önceki oturumun iddialarına kör güvenme; ilgili testleri yeniden doğrula. Tamamlanmış işi sıfırdan yazma, kullanıcı değişikliklerini ezme. İlk tamamlanmamış ve haricî izne bağlı olmayan iş paketini bulup uygula. Onay/kişisel veri/LinkedIn izinleri ve public yayın sınırlarını koru. Gerçek ilerlemeyi ve açık engelleri dosyalarda güncelleyerek devam et.

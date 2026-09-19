# Kişisel yerel kullanım

Önce [kurulum kılavuzunu](INSTALL.md) izleyin ve yerel uygulamayı açın.
“Kendi CV ve ilanım” sekmesi kişisel inceleme alanıdır. “Sentetik test” sekmesi ise
uydurma aday ve yerel test sitesiyle çalışan ayrı bir başvuru denemesidir.

1. TXT, PDF veya DOCX CV'nizi seçin. En fazla 2 MiB dosya kabul edilir.
   Taranmış PDF için OCR uygulanmaz; metin bulunamazsa işlem durur.
2. Çıkarılan metni okuyun. İsim, iletişim, deneyim türü ve tarihlerini doğrulayın;
   deneyimin dayandığı kaynak metni seçin. Stajı/projeyi profesyonel iş saymayın.
3. Maaşı para birimi, net/brüt ve dönem bilgisiyle inceleyin. Özel alt sınır,
   işverene verilecek beklenti cevabından ayrıdır.
4. İlan metnini yapıştırın ve gereklilikleri kaynak metinle doğrulayın.
   Yazılan URL otomatik açılmaz.
5. Soruya verilecek cevabı ve çekimserlik gerekçesini inceleyin. Kişisel alan
   şu sürümde işverene bağlanmaz ve gerçek başvuru göndermez.
6. Yeni bir cevap için soru, dil, kapsam ve gerekiyorsa geçerlilik tarihini seçin.
   Başvuru/şirket kapsamı seçili ilanla bağlıdır. Kaynak kanıtını ve metni inceleyip
   ayrıca doğrulayın. İlan değişirse önceki doğrulama geçersiz olur.
   Kaydedilen cevabı geri çekmek sonraki yanıtlarda kullanılmasını engeller;
   eski sürüm geçmişini silmek için çalışma alanının tümünü silmeniz gerekir.

Veri Windows kullanıcı hesabına bağlı DPAPI ile korunarak proje dışında tutulur.
Varsayılan yer `%LOCALAPPDATA%/JobApplicationAgent/demo/personal/workspace.db`.
Farklı Windows hesabına doğrudan taşınabilir bir şifreli yedek değildir.
Dışa aktarılan JSON açık kişisel bilgi içerir: güvenli saklayın, GitHub'a yüklemeyin.
Silme uygulama kayıtlarını temizler; disk üzerinde adli olarak kurtarılamaz silme
garantisi verilmez.

Gerçek bir işverene gönderim için izinli hedef, gözden geçirilmiş CV/cevap paketi
ve o pakete ait kullanıcı onayı gerekir. LinkedIn adaptörü platform izni olmadan
kapalıdır. Yerel kullanım ücretli model API'si gerektirmez.

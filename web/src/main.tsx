import { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';
import { LocalWorkspace } from './LocalWorkspace';

type Answer = { status: string; value: string | null; reason: string; evidenceIds: string[] };
type Profile = { fullName: string; email: string; version: number; facts: { id: string; value: string; sourceSpan: string; verificationStatus: string }[];
  salary: { amount: number; currency: string; period: string; taxBasis: string } };
type Application = { draft: { id: string; status: string; employer: string; jobTitle: string; recipientOrigin: string; resumeHash: string; answers: Record<string, string> };
  evidence: { receiptId: string; verifiedAt: string; resumeHash: string } | null; error: string | null };
type State = { mode: string; profileConfirmed: boolean; profile: Profile | null; resumeText: string | null; resumeHash: string;
  job: { employer: string; title: string; text: string }; evaluation: { status: string; requirements: { requirementText: string; assessment: string }[] } | null;
  answers: Record<string, Answer> | null; application: Application | null };

const statuses: Record<string, string> = {
  ReadyForDataSharing: 'Veri paylaşım onayı bekleniyor', Filling: 'Form dolduruluyor',
  AwaitingSubmissionApproval: 'Son gönderim onayı bekleniyor', Submitting: 'Gönderiliyor',
  SubmittedVerified: 'Gönderim teyit edildi', SubmittedUnverified: 'Gönderim sonucu belirsiz',
  FailedBeforeSubmission: 'Gönderim öncesinde durdu', Cancelled: 'İptal edildi', NeedsInput: 'Bilgi gerekiyor'
};
const labels: Record<string, string> = {
  'contact.name': 'Ad soyad', 'contact.email': 'E-posta',
  'salary.expected.monthly.net.TRY': 'Maaş beklentisi · TRY / ay / net',
  'experience.professional.csharp.years': 'Profesyonel C# deneyimi · yıl'
};
const errorLabels: Record<string, string> = {
  UserSessionChanged: 'Oturum değişti. Mevcut tarayıcı akışı bu oturumdan gönderilemez.',
  SubmissionOutcomeUnknown: 'Sunucunun sonucu doğrulanamadı. Tekrar gönderim kapalı.',
  BrowserSessionLost: 'Tarayıcı oturumu kapandı. Formu doldurmak için yeniden paylaşım onayı verin.',
  CancelledByUser: 'Yeni işlemler durduruldu.', FormChanged: 'Form değişti. Yeniden inceleme gerekiyor.',
  RecipientChanged: 'Hedef değiştiği için işlem durduruldu.', NeedsInput: 'Cevap için doğrulanmış bilgi eksik.',
  LocalOperationFailed: 'Yerel işlem tamamlanamadı. Kurulum ve test kayıtlarını kontrol edin.'
};

function App() {
  const [tab, setTab] = useState<'demo' | 'personal'>('demo');
  const [state, setState] = useState<State | null>(null);
  const [csrf, setCsrf] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function refresh() {
    const r = await fetch('/api/state');
    if (!r.ok) throw new Error('Yerel oturum açılmadı. Başlatma ekranındaki özel bağlantıyı kullanın.');
    setState(await r.json());
  }
  useEffect(() => {
    void (async () => {
      try {
        const token = new URLSearchParams(location.hash.slice(1)).get('token');
        history.replaceState(null, '', location.pathname);
        const r = token ? await fetch('/api/session', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ token }) })
          : await fetch('/api/session');
        if (!r.ok) throw new Error('Yerel oturum açılmadı. Başlatma ekranındaki özel bağlantıyı kullanın.');
        setCsrf((await r.json()).csrf); await refresh();
      } catch (e) { setError((e as Error).message); }
    })();
  }, []);
  async function act(path: string) {
    setBusy(true); setError('');
    try {
      const r = await fetch(path, { method: 'POST', headers: { 'X-JobAgent-Csrf': csrf, 'Content-Type': 'application/json' }, body: '{}' });
      if (!r.ok) { const e = await r.json().catch(() => ({ error: 'Oturum veya işlem yetkisi doğrulanamadı.' })); throw new Error(errorLabels[e.error] || e.error); }
      await refresh();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  const run = state?.application;
  const status = run?.draft.status;
  const confirmed = status === 'SubmittedVerified';
  const step = confirmed ? 4 : run ? 3 : state?.profileConfirmed ? 2 : 1;
  const canCancel = run && !['SubmittedVerified', 'SubmittedUnverified', 'Cancelled'].includes(status || '');
  return <>
    <header className="topbar"><div className="brand"><span className="brand-mark" aria-hidden="true">↗</span><h1>Başvuru Atölyesi</h1><span className="version">v0.1 · geliştirme</span></div>
      <span className="local"><span aria-hidden="true">●</span> Yerel çalışma alanı</span></header>
    <main>
      <nav className="workspace-tabs" aria-label="Çalışma alanı"><button className={tab === 'demo' ? '' : 'secondary'} onClick={() => setTab('demo')}>Sentetik test</button><button className={tab === 'personal' ? '' : 'secondary'} onClick={() => setTab('personal')}>Kendi CV ve ilanım</button></nav>
      <div hidden={tab !== 'personal'}><LocalWorkspace csrf={csrf} /></div>
      <div hidden={tab !== 'demo'}>
      <div className="intro"><div><p className="eyebrow">JOB APPLICATION AGENT</p><h2>Bilginiz doğru.<br />Kontrol sizde.</h2>
        <p className="lead">Profilinizi doğrulayın, hazırlanmış cevapları inceleyin.<br />Her paylaşım ve gönderim sizin onayınızla ilerler.</p></div>
        <aside className="demo-note"><strong>Sentetik deneme</strong><p>Bu akışta kurgusal bir CV ve yerel test sitesi kullanılır. Hiçbir işverene başvuru gitmez.</p><span>API anahtarı gerekmez · Ücretli çağrı yok</span></aside></div>
      <ol className="steps" aria-label="Başvuru aşamaları">{['Profil', 'İlan ve cevaplar', 'İnceleme ve onay', 'Sonuç teyidi'].map((s, i) =>
        <li key={s} className={i + 1 === step ? 'active' : i + 1 < step ? 'done' : ''}><span>{i + 1 < step ? '✓' : i + 1}</span>{s}</li>)}</ol>
      {error && <div className="error" role="alert">{error}</div>}
      {busy && <div className="working" role="status">İşlem sürüyor… Yerel tarayıcı ve kayıtlar kontrol ediliyor.</div>}
      <div className="workspace">
        <section className="panel primary"><div className="panel-heading"><div><p className="eyebrow">01 / KAYNAĞIYLA PROFİL</p><h3>Aday bilgileri</h3></div><span className={'pill ' + (state?.profileConfirmed ? 'green' : '')}>{state?.profileConfirmed ? 'Doğrulandı' : 'İnceleme gerekli'}</span></div>
          {!state?.profile ? <div className="empty"><span className="file-icon" aria-hidden="true">▤</span><h4>İlk adım: sentetik CV</h4><p>Örnek belgeyi yerel olarak içeri aktarın.<br />Gerçek kişisel bilgiler bu demoda kullanılmaz.</p>
            <button disabled={!csrf || busy} onClick={() => void act('/api/demo/load')}>Sentetik CV'yi içeri aktar</button></div> : <>
            <div className="identity"><div className="avatar" aria-hidden="true">SC</div><div><h4>{state.profile.fullName}</h4><p>{state.profile.email}</p></div></div>
            <div className="profile-grid"><div><small>MAAŞ BEKLENTİSİ</small><strong>{state.profile.salary.amount.toLocaleString('tr-TR')} TRY</strong><span>Aylık · Net · Sentetik tutar</span></div><div><small>DOĞRULANACAK DENEYİM</small><strong>C# · 3 yıl</strong><span>2023–2026 · Profesyonel</span></div></div>
            <p className="quiet">Özel maaş alt sınırı cevap paketine dahil edilmez. Kişisel Java projesi profesyonel deneyim sayılmaz.</p>
            <details><summary>CV metnini ve kaynak kanıtını incele</summary><pre>{state.resumeText || 'Kaynak belge daha önce bu cihazda incelendi ve profil kaydedildi.'}</pre>
              {state.profile.facts.map(f => <p className="source" key={f.id}>{f.value} · {f.sourceSpan} · {f.verificationStatus === 'Verified' ? 'Doğrulandı' : 'Öneri'}</p>)}</details>
            {!state.profileConfirmed && <button disabled={busy} onClick={() => void act('/api/profile/confirm')}>Profili doğrula</button>}
          </>}
        </section>
        <aside className="panel job"><p className="eyebrow">02 / SEÇİLİ SENTETİK İLAN</p><div className="employer-icon" aria-hidden="true">SL</div><h3>{state?.job.title || '.NET Developer'}</h3><p className="employer">{state?.job.employer || 'Synthetic Labs'}</p><span className="pill">Uzaktan · Test ilanı</span><hr /><p>En az 2 yıl profesyonel C# deneyimi.</p>
          {state?.evaluation && <div className="fit"><span aria-hidden="true">✓</span><div><strong>{state.evaluation.status === 'Eligible' ? 'Kayıtlı koşullar eşleşiyor' : 'Koşullar incelenmeli'}</strong><p>Kaynaklı profesyonel deneyim değerlendirildi. Bu sonuç işe alınma olasılığı değildir.</p></div></div>}
          {state?.profileConfirmed && !run && <button disabled={busy} onClick={() => void act('/api/applications')}>İlanı değerlendir ve cevapları hazırla</button>}
          <p className="quiet">LinkedIn otomasyonu için platform izni doğrulanmadı. Bu demoda bağlantı kurulmaz.</p>
        </aside>
        {run && <section className="panel review"><div className="panel-heading"><div><p className="eyebrow">03 / GÖNDERİLECEK PAKET</p><h3>Cevaplar, CV ve alıcı</h3></div><span className={'pill ' + (confirmed ? 'green' : 'amber')} data-testid="application-status">{statuses[status!] || status}</span></div>
          <div className="answer-grid">{Object.entries(run.draft.answers).map(([key, value]) => <div key={key}><small>{labels[key] || key}</small><strong data-testid={key.startsWith('salary.') ? 'salary-answer' : undefined}>{value}</strong><span>Doğrulanmış profil · sürüm {state?.profile?.version}</span></div>)}</div>
          <div className="package"><div><small>ALICI</small><strong>{run.draft.employer} · {run.draft.jobTitle}</strong><code>{run.draft.recipientOrigin}</code></div><div><small>YÜKLENECEK CV</small><strong>synthetic-resume.txt</strong><code title={run.draft.resumeHash}>SHA-256: {run.draft.resumeHash.slice(0, 24)}…</code></div></div>
          {run.error && <p className="error" role="alert">{errorLabels[run.error] || run.error}</p>}
          {status === 'ReadyForDataSharing' && <div className="approval"><p>Ad, e-posta, maaş beklentisi, deneyim cevabı ve bu CV yalnız yukarıdaki yerel test sitesiyle paylaşılacak.</p><button disabled={busy} onClick={() => void act('/api/approve-share')}>Veri paylaşımını onayla ve formu doldur</button></div>}
          {status === 'AwaitingSubmissionApproval' && <div className="approval"><p>Tarayıcı formu doldurdu ve CV’yi seçti. Paketi inceledikten sonra gönderimi ayrıca onaylayın.</p><button disabled={busy} onClick={() => void act('/api/approve-submit')}>Bu sentetik başvuruyu gönder</button></div>}
          {confirmed && run.evidence && <div className="receipt"><div className="receipt-check" aria-hidden="true">✓</div><div><h4>Test sunucusu başvuruyu aldı.</h4><p>Başvuru kimliği ve yüklenen CV’nin özeti sunucu yanıtıyla eşleşti.</p><code data-testid="receipt-id">{run.evidence.receiptId}</code><p className="quiet">{new Date(run.evidence.verifiedAt).toLocaleString('tr-TR')} · Yerel sentetik sonuç</p></div></div>}
          {canCancel && <button className="secondary" onClick={() => void act('/api/cancel')}>İşlemi iptal et</button>}
        </section>}
      </div>
      </div>
      <footer><span>Veri cihazınızda · Kaynaklı cevaplar · Ayrı paylaşım ve gönderim onayı</span><span>Yerel demo / Fixture</span></footer>
    </main>
  </>;
}

createRoot(document.getElementById('root')!).render(<App />);

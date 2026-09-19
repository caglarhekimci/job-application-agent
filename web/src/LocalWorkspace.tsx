import { useEffect, useState, type FormEvent } from 'react';
import { Applications, type ImportedJobProposal } from './Applications';
import { ModelPolicySettings } from './ModelPolicySettings';

type Experience = { sourceSpan: string; start: string; end: string | null; role: string; kind: string; skills: string[] };
type Requirement = { id: string; requirementText: string; type: string; importance: string; skill: string; minimumYears: number | null };
type Memory = { semanticKey: string; answer: string; scope: string; scopeId: string | null; language: string; expiresAt: string | null };
type Workspace = { revision: number; fileName: string | null; salaryPrivateMinimum: number | null;
  document: { text: string; status: string; fileHash: string; evidenceSegments: { sourceSpan: string; text: string }[] } | null;
  profile: { fullName: string; email: string; version: number; verifiedAt: string | null; facts: { id: string; sourceSpan: string }[];
    experience: (Experience & { evidenceIds: string[] })[]; answers: Memory[]; salary: { confirmedAt: string | null; target: { amount: number }; privateMinimum: { amount: number } } };
  job: { employer: string; title: string; text: string; sourceUrl: string; requirements: Requirement[] } | null;
  evaluation: { status: string; requirements: { requirementText: string; assessment: string; reason: string }[] } | null };
type Answer = { status: string; value: string | null; reason: string; evidenceIds: string[] };
const answerNames: Record<string, string> = { Resolved: 'Doğrulanmış cevap', NeedsInput: 'Bilgi eksik', RequiresReview: 'İnceleme gerekli', ManualOnly: 'Kendiniz yanıtlayın', Blocked: 'Kapalı' };
const assessmentNames: Record<string, string> = { Match: 'Eşleşiyor', Mismatch: 'Eşleşmiyor', Unknown: 'Bilgi eksik', NotApplicable: 'Uygulanmaz' };
const evaluationNames: Record<string, string> = { Eligible: 'İncelenen koşullar eşleşiyor', ReviewNeeded: 'İnceleme gerekiyor', HardRequirementMismatch: 'Zorunlu koşul karşılanmıyor', InsufficientInformation: 'Bilgi yetersiz', Closed: 'İlan kapalı' };
const memoryQuestionNames: Record<string, string> = { motivation: 'Bu rolü neden istiyorsunuz?', availability: 'Ne zaman başlayabilirsiniz?' };

export function LocalWorkspace({ csrf }: { csrf: string }) {
  const [state, setState] = useState<Workspace | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [name, setName] = useState(''); const [email, setEmail] = useState('');
  const [salary, setSalary] = useState(''); const [minimum, setMinimum] = useState('');
  const [experience, setExperience] = useState<Experience[]>([]);
  const [employer, setEmployer] = useState(''); const [title, setTitle] = useState('');
  const [jobText, setJobText] = useState(''); const [sourceUrl, setSourceUrl] = useState('');
  const [requirements, setRequirements] = useState<Requirement[]>([]);
  const [jobProposalRef, setJobProposalRef] = useState<string | null>(null);
  const [question, setQuestion] = useState('contact.name'); const [answer, setAnswer] = useState<Answer | null>(null);
  const [deleteConfirmed, setDeleteConfirmed] = useState(false);
  const [memoryQuestion, setMemoryQuestion] = useState(''); const [memoryType, setMemoryType] = useState('motivation'); const [memoryAnswer, setMemoryAnswer] = useState('');
  const [memoryScope, setMemoryScope] = useState('Company'); const [memoryLanguage, setMemoryLanguage] = useState('tr');
  const [memoryExpiry, setMemoryExpiry] = useState(''); const [memoryConfirmedRevision, setMemoryConfirmedRevision] = useState<number | null>(null);
  const memoryConfirmed = state !== null && memoryConfirmedRevision === state.revision;
  const setMemoryConfirmed = (value: boolean) => setMemoryConfirmedRevision(value ? state?.revision ?? null : null);
  useEffect(() => {
    if (!csrf) return;
    void fetch('/api/workspace').then(async r => {
      if (!r.ok) throw new Error('Yerel çalışma alanı açılamadı.');
      const s: Workspace = await r.json(); setState(s); setName(s.profile.fullName); setEmail(s.profile.email);
      setExperience(s.profile.experience.map(p => ({ ...p, sourceSpan: s.profile.facts.find(f => p.evidenceIds.includes(f.id))?.sourceSpan || '' })));
      if (s.profile.salary.confirmedAt) setSalary(String(s.profile.salary.target.amount));
      setMinimum(s.salaryPrivateMinimum === null ? '' : String(s.salaryPrivateMinimum));
      if (s.job) { setEmployer(s.job.employer); setTitle(s.job.title); setJobText(s.job.text); setSourceUrl(s.job.sourceUrl); setRequirements(s.job.requirements); }
    }).catch(e => setError((e as Error).message));
  }, [csrf]);
  async function action(path: string, body: unknown, file?: File) {
    setBusy(true); setError(''); setAnswer(null);
    try {
      const headers: Record<string, string> = { 'X-JobAgent-Csrf': csrf, 'Content-Type': file ? 'application/octet-stream' : 'application/json' };
      if (file) headers['X-File-Name'] = encodeURIComponent(file.name);
      const r = await fetch('/api/workspace/' + path, { method: 'POST', headers, body: file || JSON.stringify(body) });
      if (!r.ok) {
        const result = await r.json().catch(() => ({ error: 'LocalOperationFailed' }));
        throw new Error(result.error === 'ParsingTimedOut' ? 'Belge zaman sınırını aştı. Daha küçük veya düz metin bir dosya deneyin.'
          : result.error === 'ContentTypeMismatch' ? 'Dosyanın içeriği uzantısıyla eşleşmiyor.'
          : 'İşlem tamamlanamadı. Alanları, dosyayı ve tarihleri kontrol edin; sayfa eskiyse yenileyin. (' + result.error + ')');
      }
      if (path === 'answer') setAnswer(await r.json());
      else if (r.status !== 204) setState(await r.json());
      else { setState(await (await fetch('/api/workspace')).json()); setName(''); setEmail(''); setSalary(''); setMinimum(''); setExperience([]); setEmployer(''); setTitle(''); setJobText(''); setSourceUrl(''); setRequirements([]); setDeleteConfirmed(false); }
      if (path === 'answer-memory') { setMemoryAnswer(''); setMemoryConfirmed(false); }
      if (path.startsWith('job-proposals/')) setJobProposalRef(null);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  function submitProfile(e: FormEvent) {
    e.preventDefault(); void action('profile', { expectedRevision: state?.revision, fullName: name, email,
      salaryTarget: salary === '' ? null : Number(salary), salaryPrivateMinimum: minimum === '' ? null : Number(minimum), experience });
  }
  const setPeriod = (index: number, patch: Partial<Experience>) => setExperience(items => items.map((p, i) => i === index ? { ...p, ...patch } : p));
  const setRequirement = (index: number, patch: Partial<Requirement>) => setRequirements(items => items.map((r, i) => i === index ? { ...r, ...patch } : r));
  async function reviewJobProposal(proposal: ImportedJobProposal) {
    try {
      const response = await fetch('/api/workspace');
      if (!response.ok) throw new Error('Güncel çalışma alanı alınamadı.');
      setState(await response.json());
      setJobProposalRef(proposal.posting.id); setEmployer(proposal.posting.employer); setTitle(proposal.posting.title);
      setJobText(proposal.posting.text); setSourceUrl(proposal.posting.sourceUrl);
      setRequirements(proposal.suggestedRequirements.map(r => ({ ...r, skill: r.skill || '' })));
      setMemoryConfirmed(false);
    } catch (e) { setError((e as Error).message); }
  }
  return <div className="local-workspace">
    <section className="panel"><p className="eyebrow">KENDİ ÇALIŞMA ALANINIZ</p><h3>CV’nizden kontrollü cevaplara</h3>
      <p>Dosyanız ve profiliniz bu Windows hesabında korumalı olarak saklanır. Bu ekranda dış sitelere bağlantı kurulmaz; ilan metnini siz eklersiniz.</p>
      {error && <p role="alert" className="error">{error}</p>}
      {busy && <p role="status">İşlem sürüyor…</p>}
      <label className="field">CV dosyanız · TXT, PDF veya DOCX · en fazla 2 MiB
        <input aria-label="CV dosyanız" type="file" accept=".txt,.pdf,.docx" disabled={busy || !csrf} onChange={e => {
          const file = e.target.files?.[0]; if (file) { setExperience([]); void action('import', null, file); } e.target.value = '';
        }} /></label>
      {state?.document && <><p><strong>{state.fileName}</strong> · {state.document.status === 'NeedsOcr' ? 'Metin çıkarılamadı. Metin içeren bir CV yükleyin.' : 'Belge incelemeye hazır'}</p>
        <details><summary>Belge metnini ve kaynak bölümlerini incele</summary><pre>{state.document.text}</pre></details></>}
    </section>
    {state && <ModelPolicySettings csrf={csrf} workspaceRevision={state.revision}
      onChanged={async () => { const response = await fetch('/api/workspace'); if (!response.ok) throw new Error('Çalışma alanı yenilenemedi.'); setState(await response.json()); }} />}
    {state?.document?.status === 'ReadyForReview' && <section className="panel"><p className="eyebrow">01 / YEREL PROFİL</p><h3>Bilgilerinizi inceleyip doğrulayın</h3>
      <p className="quiet">Ad, e-posta, maaş ve deneyim tarihlerini siz girersiniz. Belgedeki bir cümle kendiliğinden doğrulanmış bilgi sayılmaz.</p>
      <form onSubmit={submitProfile}>
        <div className="form-grid"><label className="field">Ad soyad<input required maxLength={200} value={name} onChange={e => setName(e.target.value)} /></label>
          <label className="field">E-posta<input required type="email" value={email} onChange={e => setEmail(e.target.value)} /></label>
          <label className="field">Maaş beklentisi · aylık net TRY<input type="number" min="0" max="1000000000" value={salary} onChange={e => setSalary(e.target.value)} placeholder="Belirtmek istemiyorsanız boş bırakın" /></label>
          <label className="field">Özel alt sınır · cevaplara eklenmez<input type="number" min="0" value={minimum} onChange={e => setMinimum(e.target.value)} /></label></div>
        <h4>Kaynağıyla deneyim</h4>
        {experience.map((p, i) => <fieldset key={i}><legend>Deneyim {i + 1}</legend><div className="form-grid">
          <label className="field">CV’deki kaynak bölüm<select required value={p.sourceSpan} onChange={e => setPeriod(i, { sourceSpan: e.target.value })}><option value="">İlgili bölümü seçin</option>{state.document!.evidenceSegments.map(s => <option key={s.sourceSpan} value={s.sourceSpan}>{s.sourceSpan} · {s.text.slice(0, 180)}</option>)}</select></label>
          <label className="field">Görev<input required value={p.role} onChange={e => setPeriod(i, { role: e.target.value })} /></label>
          <label className="field">Başlangıç<input required type="date" value={p.start} onChange={e => setPeriod(i, { start: e.target.value })} /></label>
          <label className="field">Bitiş · sürüyorsa boş bırakın<input type="date" value={p.end || ''} onChange={e => setPeriod(i, { end: e.target.value || null })} /></label>
          <label className="field">Deneyim türü<select value={p.kind} onChange={e => setPeriod(i, { kind: e.target.value })}><option value="Professional">Profesyonel iş</option><option value="Internship">Staj</option><option value="PartTime">Yarı zamanlı</option><option value="PersonalProject">Kişisel proje</option></select></label>
          <label className="field">Beceriler · virgülle ayırın<input required value={p.skills.join(', ')} onChange={e => setPeriod(i, { skills: e.target.value.split(',').map(v => v.trim()) })} /></label></div>
          <button className="secondary" type="button" onClick={() => setExperience(items => items.filter((_, n) => n !== i))}>Bu deneyimi kaldır</button></fieldset>)}
        <button className="secondary" type="button" onClick={() => setExperience(items => [...items, { sourceSpan: '', start: '', end: null, role: '', kind: 'Professional', skills: [] }])}>Deneyim ekle</button>
        <p className="quiet">Kaydetmek, yukarıdaki alanların ve seçtiğiniz kaynakların doğruluğunu yerel olarak onaylar. Bu işlem bir işverene gönderim yapmaz.</p>
        <button disabled={busy} type="submit">Bu profil bilgilerini doğrula ve kaydet</button>
        {state.profile.verifiedAt && <p className="pill green" role="status">Profil kaydedildi · sürüm {state.profile.version}</p>}
      </form>
    </section>}
    {state?.profile.verifiedAt && <section className="panel"><p className="eyebrow">02 / İLANI İNCELE</p><h3>İlan metni ve koşulları</h3>
      {jobProposalRef && <p role="status">Modelin ilan önerisi inceleme alanına alındı. Metni ve koşulları kontrol edip aşağıdan doğrulayın.</p>}
      <form onSubmit={e => { e.preventDefault(); void action(jobProposalRef ? 'job-proposals/' + jobProposalRef + '/review' : 'job', { expectedRevision: state.revision, employer, title, text: jobText, sourceUrl, requirements }); }}>
      <div className="form-grid"><label className="field">İşveren<input required value={employer} onChange={e => setEmployer(e.target.value)} /></label>
        <label className="field">İlan başlığı<input required value={title} onChange={e => setTitle(e.target.value)} /></label></div>
      <label className="field">Kaynak bağlantısı · yalnız kayıt için, açılmaz<input type="url" value={sourceUrl} onChange={e => setSourceUrl(e.target.value)} /></label>
      <label className="field">İlan metni<textarea required rows={7} maxLength={100000} value={jobText} onChange={e => setJobText(e.target.value)} /></label>
      {requirements.map((r, i) => <fieldset key={r.id}><legend>Koşul {i + 1}</legend><label className="field">İlandaki koşul<input required value={r.requirementText} onChange={e => setRequirement(i, { requirementText: e.target.value })} /></label>
        <div className="form-grid"><label className="field">Koşul türü<select value={r.type} onChange={e => setRequirement(i, { type: e.target.value })}><option value="ProfessionalExperienceYears">Profesyonel deneyim süresi</option><option value="WorkAuthorization">Çalışma izni</option><option value="Language">Dil</option><option value="Skill">Beceri</option><option value="Location">Konum</option><option value="Contract">Sözleşme türü</option></select></label>
          <label className="field">Önemi<select value={r.importance} onChange={e => setRequirement(i, { importance: e.target.value })}><option value="Mandatory">Zorunlu</option><option value="Preferred">Tercih sebebi</option></select></label>
          {r.type === 'ProfessionalExperienceYears' && <><label className="field">Beceri<input required value={r.skill} onChange={e => setRequirement(i, { skill: e.target.value })} /></label><label className="field">En az kaç yıl<input required type="number" min="0" max="80" step="0.1" value={r.minimumYears ?? ''} onChange={e => setRequirement(i, { minimumYears: e.target.value === '' ? null : Number(e.target.value) })} /></label></>}</div>
        <button className="secondary" type="button" onClick={() => setRequirements(items => items.filter((_, n) => n !== i))}>Koşulu kaldır</button></fieldset>)}
      <button className="secondary" type="button" onClick={() => setRequirements(items => [...items, { id: crypto.randomUUID(), requirementText: '', type: 'ProfessionalExperienceYears', importance: 'Mandatory', skill: '', minimumYears: null }])}>Koşul ekle</button>
      <p className="quiet">Otomatik hesaplama şu anda kaynaklı profesyonel deneyim süresini değerlendirir. Diğer koşullar bilgi eksik olarak gösterilir. Tüm zorunlu koşulları eklediğinizi kontrol edin.</p>
      <button disabled={busy} type="submit">İlan koşullarını doğrula ve değerlendir</button></form>
      {state.evaluation && <div className="fit"><div><strong data-testid="local-evaluation">{evaluationNames[state.evaluation.status] || state.evaluation.status}</strong>{state.evaluation.requirements.map((r, i) => <p key={i}>{r.requirementText} · {assessmentNames[r.assessment] || r.assessment}</p>)}</div></div>}
    </section>}
    {state?.profile.verifiedAt && <section className="panel"><p className="eyebrow">03 / CEVAP ÖNİZLEMESİ</p><h3>Profiliniz hangi cevabı destekliyor?</h3>
      <label className="field">Soru<select aria-label="Soru" value={question} onChange={e => { setQuestion(e.target.value); setAnswer(null); }}><option value="contact.name">Ad soyad</option><option value="contact.email">E-posta</option><option value="salary.expected.monthly.net.TRY">Beklenen aylık net maaş · TRY</option><option value="salary.current">Mevcut maaş</option><option value="experience.professional.csharp.years">Profesyonel C# deneyimi · yıl</option><option value="work.authorization">Çalışma izni</option></select></label>
      <button disabled={busy} onClick={() => void action('answer', { key: question, language: 'tr' })}>Cevabı kontrol et</button>
      {answer && <div className="answer-preview" data-testid="local-answer"><strong>{answerNames[answer.status]}</strong><p>{answer.value || 'Eksik bilgi yerine bir cevap üretilmedi.'}</p><small>{answer.evidenceIds.length} kaynak kaydı</small></div>}
      <p className="quiet">Bu alandaki cevaplar dışarı gönderilmez. Tarayıcıda gönderim denemesi için üstteki sentetik test sekmesini kullanın.</p>
    </section>}
    {state?.profile.verifiedAt && <section className="panel"><p className="eyebrow">04 / CEVAP HAFIZASI</p><h3>Yeni bir cevabı nerede hatırlayalım?</h3>
      <p>Soru türünü seçin ve kendi incelediğiniz cevabı yazın. Kapsam, bu cevabın hangi ilanda yeniden kullanılacağını sınırlar. Buraya hassas kimlik veya sağlık bilgisi eklemeyin.</p>
      <p>Yalnız bir başvuruya ait cevapları aşağıdaki başvuru taslağında kaydedin. Bu bölüm şirket veya genel kapsam içindir.</p>
      <form onSubmit={e => { e.preventDefault(); if (memoryConfirmed) void action('answer-memory', {
        expectedRevision: memoryConfirmedRevision, semanticKey: memoryType === 'custom' ? 'custom:' + memoryQuestion.trim() : memoryType, answer: memoryAnswer, scope: memoryScope,
        language: memoryLanguage, expiresAt: memoryExpiry ? new Date(memoryExpiry + 'T23:59:59').toISOString() : null
      }); }}>
        <label className="field">Hatırlanacak soru<select aria-label="Hatırlanacak soru" value={memoryType} onChange={e => { setMemoryType(e.target.value); setMemoryConfirmed(false); }}>
          {Object.entries(memoryQuestionNames).map(([key, label]) => <option key={key} value={key}>{label}</option>)}<option value="custom">Başka bir soru · yalnız aynı metinle eşleşir</option></select></label>
        {memoryType === 'custom' && <label className="field">Özel sorunun tam metni<input required maxLength={193} value={memoryQuestion} onChange={e => { setMemoryQuestion(e.target.value); setMemoryConfirmed(false); }} /></label>}
        <label className="field">İncelediğiniz cevap<textarea required maxLength={4000} rows={3} value={memoryAnswer} onChange={e => { setMemoryAnswer(e.target.value); setMemoryConfirmed(false); }} /></label>
        <div className="form-grid"><label className="field">Cevabın kapsamı<select aria-label="Cevabın kapsamı" value={memoryScope} onChange={e => { setMemoryScope(e.target.value); setMemoryConfirmed(false); }}>
          <option value="Company" disabled={!state.job}>Yalnız bu şirket</option>
          <option value="Default">Genel · diğer ilanlarda da kullanılabilir</option></select></label>
          <label className="field">Cevabın dili<select aria-label="Cevabın dili" value={memoryLanguage} onChange={e => { setMemoryLanguage(e.target.value); setMemoryConfirmed(false); }}><option value="tr">Türkçe</option><option value="en">English</option></select></label>
          <label className="field">Geçerlilik sonu · isteğe bağlı<input type="date" value={memoryExpiry} onChange={e => { setMemoryExpiry(e.target.value); setMemoryConfirmed(false); }} /></label></div>
        <p className="quiet">{memoryScope === 'Default' ? 'Bu cevap tüm şirketlerin aynı sorusu için kullanılabilir.' : state.job ? 'Kapsam: ' + state.job.employer + (memoryScope === 'Application' ? ' · ' + state.job.title : '') : 'Önce bir ilan inceleyin veya genel kapsamı seçin.'}</p>
        <label className="delete-check"><input type="checkbox" checked={memoryConfirmed} onChange={e => setMemoryConfirmed(e.target.checked)} /> Cevabı ve seçtiğim kapsamı inceledim.</label>
        <button type="submit" disabled={busy || !memoryConfirmed || (!state.job && memoryScope !== 'Default')}>Cevabı bu kapsamda hatırla</button>
      </form>
      <div data-testid="answer-memory">{state.profile.answers.length === 0 ? <p>Henüz hatırlanan cevap yok.</p> : state.profile.answers.map(a => <article key={[a.semanticKey, a.scope, a.scopeId, a.language].join('|')}>
        <h4>{memoryQuestionNames[a.semanticKey] || a.semanticKey.replace(/^custom:/, '')}</h4><p>{a.answer}</p><p className="quiet">{a.scope === 'Default' ? 'Genel' : a.scope === 'Company' ? 'Şirket: ' + a.scopeId : a.scope === 'RoleGroup' ? 'Seçilen rol grubu' : 'Başvuruya özel · ilgili taslakta inceleyin'} · {a.language}{a.expiresAt ? ' · ' + new Date(a.expiresAt).toLocaleDateString('tr-TR') + ' tarihine kadar' : ''}</p>
        <button className="secondary" disabled={busy || a.scope === 'Application'} onClick={() => void action('answer', { key: a.semanticKey, language: a.language })}>Bu ilan için kontrol et</button>
        <button className="secondary" disabled={busy} onClick={() => void action('answer-memory/revoke', { expectedRevision: state.revision, key: { semanticKey: a.semanticKey, language: a.language, scope: a.scope, scopeId: a.scopeId } })}>Bu cevabın kullanımını kaldır</button>
      </article>)}</div>
      <p className="quiet">Kullanımı kaldırmak önceki sürüm geçmişini silmez. Tüm geçmişi temizlemek için alttaki yerel veri silme işlemini kullanın. Yeni CV veya profil incelemesi bu cevapları yeniden doğrulamanızı gerektirir.</p>
    </section>}
    {state && <Applications csrf={csrf} workspaceRevision={state.revision} profileConfirmed={!!state.profile.verifiedAt}
      onChanged={async () => { const response = await fetch('/api/workspace'); if (!response.ok) throw new Error('Çalışma alanı yenilenemedi.'); setState(await response.json()); }}
      onJobProposal={reviewJobProposal} />}
    {state?.document && <section className="panel"><h3>Verilerinizin kontrolü</h3><p>Dışa aktarım CV’nizi, profilinizi ve sürüm geçmişinizi içerir. İndirdiğiniz dosya şifrelenmez; güvenli bir yerde saklayın.</p>
      <a className="download-link" href="/api/workspace/export" download>Yerel verilerimi indir</a>
      <label className="delete-check"><input type="checkbox" checked={deleteConfirmed} onChange={e => setDeleteConfirmed(e.target.checked)} /> CV, profil, ilan ve önceki sürümlerin bu çalışma alanından silinmesini istiyorum.</label>
      <button className="secondary" disabled={!deleteConfirmed || busy} onClick={() => void action('delete', { expectedRevision: state.revision })}>Yerel verilerimi sil</button>
    </section>}
  </div>;
}

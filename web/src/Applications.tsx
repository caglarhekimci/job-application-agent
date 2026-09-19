import { useEffect, useRef, useState, type FormEvent } from 'react';
import { DocumentAdaptation } from './DocumentAdaptation';

type Question = { key: string; label: string; language: string; maxLength: number | null; sensitive?: boolean; requiresCandidateAttestation?: boolean };
type Resolution = { question: Question; answer: { status: string; value: string | null; reason: string } };
type Application = { workspaceRevision: number; payloadHash: string; roleGroupId: string; sourcesCurrent: boolean; reviewRequested: boolean;
  draft: { id: string; jobTitle: string; employer: string; status: string; questions: Question[] };
  questions: Resolution[]; proposals: { semanticKey: string; proposedValue: string; rationale: string }[] };
export type ImportedJobProposal = { posting: { id: string; employer: string; title: string; text: string; sourceUrl: string };
  suggestedRequirements: { id: string; requirementText: string; type: string; importance: string; skill: string | null; minimumYears: number | null }[] };
type Panel = { revision: number; references: { profileRef: string | null; resumeRef: string | null; jobRef: string | null };
  applications: Application[]; jobProposals: ImportedJobProposal[];
  profileProposals: { id: string; status: string; request: { changes: { field: string; value: string }[] } }[] };
const statusNames: Record<string, string> = { NeedsInput: 'Bilgi bekliyor', ReadyForDataSharing: 'Yerel incelemeye hazır', Cancelled: 'İptal edildi', BlockedPermission: 'Platform izni bekliyor' };
const profileFieldNames: Record<string, string> = { FullName: 'Ad soyad', Email: 'E-posta', Locale: 'Dil', ProfessionalSkill: 'Mesleki beceri' };
const groups = [['', 'Rol grubu seçilmedi'], ['dotnet-developer', '.NET geliştirme'], ['software-development', 'Yazılım geliştirme'], ['data-analysis', 'Veri analizi'], ['design', 'Tasarım']];

export function Applications({ csrf, workspaceRevision, profileConfirmed, onChanged, onJobProposal }: {
  csrf: string; workspaceRevision: number; profileConfirmed: boolean; onChanged: () => Promise<void>; onJobProposal: (proposal: ImportedJobProposal) => void;
}) {
  const [panel, setPanel] = useState<Panel | null>(null); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(''); const [group, setGroup] = useState(''); const busyRef = useRef(false);
  async function refresh() {
    const result = await fetch('/api/workspace/application-panel');
    if (!result.ok) throw new Error('Başvuru kayıtları açılamadı.');
    const next: Panel = await result.json(); setPanel(previous => previous && previous.revision > next.revision ? previous : next);
  }
  useEffect(() => {
    let active = true; const controller = new AbortController();
    async function load() {
      if (busyRef.current) return;
      try {
        const response = await fetch('/api/workspace/application-panel', { signal: controller.signal });
        if (!response.ok) throw new Error('Başvuru kayıtları açılamadı.');
        const next: Panel = await response.json();
        if (active && !busyRef.current) setPanel(previous => previous && previous.revision > next.revision ? previous : next);
      } catch (e) { if (active && !controller.signal.aborted) setError((e as Error).message); }
    }
    void load(); const timer = window.setInterval(() => void load(), 3000);
    return () => { active = false; controller.abort(); window.clearInterval(timer); };
  }, [csrf, workspaceRevision]);
  async function perform(path: string, body: unknown) {
    busyRef.current = true; setBusy(true); setError('');
    try {
      const response = await fetch('/api/workspace/' + path, { method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-JobAgent-Csrf': csrf }, body: JSON.stringify(body) });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({ error: 'LocalOperationFailed' }));
        throw new Error(problem.error === 'ProfileFieldReviewRequired' ? 'Bu bilgi için yukarıdaki profil alanını inceleyin.'
          : 'Kayıt tamamlanamadı. Kaynak veya sürüm değişmiş olabilir; sayfayı yenileyip tekrar inceleyin.');
      }
      await refresh(); await onChanged();
    } catch (e) { setError((e as Error).message); }
    finally { busyRef.current = false; setBusy(false); }
  }
  return <section className="panel"><p className="eyebrow">05 / BAŞVURU TASLAKLARI</p><h3>Eksik soruları tamamlayıp devam edin</h3>
    <p>Cevaplar aynı başvuru kaydında tutulur. Bu kişisel çalışma alanının canlı işveren bağlantısı yoktur; buradan dışarı gönderim yapılmaz.</p>
    {error && <p role="alert" className="error">{error}</p>}
    {panel?.profileProposals.map(proposal => <details key={proposal.id}><summary>İnceleme bekleyen profil önerisi{proposal.status === 'Conflict' ? ' · eski profil sürümü' : ''}</summary>
      <p>Öneri profilinize uygulanmadı. İlgili alanı yukarıdaki profil ekranında düzenleyip doğrulayın.</p>
      {proposal.request.changes.map((change, index) => <p key={index}>{profileFieldNames[change.field] || change.field}: {change.value}</p>)}
      <button className="secondary" disabled={busy} onClick={() => void perform('proposals/' + proposal.id + '/discard', { expectedRevision: panel.revision })}>Bu profil önerisini kaldır</button>
    </details>)}
    {panel?.jobProposals.map(proposal => <details key={proposal.posting.id}><summary>İlan önerisi: {proposal.posting.title}</summary>
      <p>{proposal.posting.employer}</p><pre className="source-text">{proposal.posting.text}</pre>
      <button className="secondary" disabled={busy} onClick={() => onJobProposal(proposal)}>Bu ilan önerisini incele</button>
      <button className="secondary" disabled={busy} onClick={() => void perform('proposals/' + proposal.posting.id + '/discard', { expectedRevision: panel.revision })}>Bu ilan önerisini kaldır</button>
    </details>)}
    <label className="field">Yeni başvurunun rol grubu · isteğe bağlı<select value={group} onChange={e => setGroup(e.target.value)}>
      {groups.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
    </select></label>
    <button disabled={busy || !profileConfirmed || !panel?.references.jobRef || !panel.references.resumeRef}
      onClick={() => void perform('applications', { expectedRevision: panel?.revision, questions: null, roleGroupId: group })}>Başvuru taslağı oluştur</button>
    {!profileConfirmed && <p>Önce profilinizi doğrulayın.</p>}
    {panel?.applications.map(application => <ApplicationCard key={application.draft.id} application={application}
      csrf={csrf} revision={panel.revision} busy={busy} perform={perform} />)}
  </section>;
}

function ApplicationCard({ application: app, csrf, revision, busy, perform }: {
  application: Application; csrf: string; revision: number; busy: boolean; perform: (path: string, body: unknown) => Promise<void>;
}) {
  const [key, setKey] = useState('motivation'); const [answer, setAnswer] = useState('');
  const [scope, setScope] = useState('Application'); const [expiry, setExpiry] = useState('');
  const [confirmedRevision, setConfirmedRevision] = useState<number | null>(null);
  const [newQuestion, setNewQuestion] = useState(''); const [language, setLanguage] = useState('tr');
  const [manual, setManual] = useState(false);
  const selected = app.questions.find(item => item.question.key === key);
  const selectedQuestion = selected?.question;
  const cancelled = app.draft.status === 'Cancelled';
  const reviewed = confirmedRevision === revision;
  const profileField = /^(contact|salary|experience)\./i.test(key);
  const editable = app.sourcesCurrent && !cancelled && selectedQuestion && !selectedQuestion.sensitive && !selectedQuestion.requiresCandidateAttestation && !profileField;
  function saveAnswer(event: FormEvent) {
    event.preventDefault();
    if (!reviewed || !editable) return;
    void perform('applications/answers', { applicationRef: app.draft.id, expectedRevision: revision,
      expectedPayloadHash: app.payloadHash, answers: [{ semanticKey: key, answer, language: selectedQuestion.language,
        scope, expiresAt: expiry ? new Date(expiry + 'T23:59:59').toISOString() : null }] });
  }
  return <article className="answer-preview" data-testid="personal-application" data-application-id={app.draft.id}>
    <h4>{app.draft.jobTitle} · {app.draft.employer}</h4>
    <strong data-testid="application-status">{statusNames[app.draft.status] || app.draft.status}</strong>
    <dl>{app.questions.map(item => <div key={item.question.key}><dt>{item.question.label}</dt><dd>{item.answer.value ||
      (item.answer.status === 'ManualOnly' ? 'Kendiniz yanıtlamanız gerekiyor.' : 'İnceleme veya bilgi gerekiyor.')}</dd></div>)}</dl>
    {!app.sourcesCurrent && !cancelled && <div><p>Profil, CV veya ilan kaynağı değişti. Yukarıdaki kaynakları inceleyip bu başvuruya yeniden bağlayın.</p>
      <button className="secondary" disabled={busy} onClick={() => void perform('applications/' + app.draft.id + '/refresh', { expectedRevision: revision })}>İncelenen güncel kaynaklarla yenile</button></div>}
    {!cancelled && <>
      {app.proposals.map(proposal => <blockquote key={proposal.semanticKey}><p>Model önerisi · henüz doğrulanmadı</p><p>{proposal.proposedValue}</p>
        <button className="secondary" disabled={busy} onClick={() => { setKey(proposal.semanticKey); setAnswer(proposal.proposedValue); setConfirmedRevision(null); }}>Cevap alanında incele</button></blockquote>)}
      <form onSubmit={saveAnswer}>
        <label className="field">Yanıtlanacak soru<select aria-label="Yanıtlanacak soru" value={key} onChange={e => { setKey(e.target.value); setAnswer(''); setConfirmedRevision(null); }}>
          {!app.questions.some(item => item.question.key === key) && <option value={key}>Bir soru seçin</option>}
          {app.questions.map(item => <option key={item.question.key} value={item.question.key}>{item.question.label}</option>)}
        </select></label>
        <label className="field">Başvuru cevabınız<textarea required rows={3} maxLength={selectedQuestion?.maxLength || 4000} value={answer}
          disabled={!editable} onChange={e => { setAnswer(e.target.value); setConfirmedRevision(null); }} /></label>
        <label className="field">Başvuru cevabının kapsamı<select aria-label="Başvuru cevabının kapsamı" value={scope} onChange={e => { setScope(e.target.value); setConfirmedRevision(null); }}>
          <option value="Application">Yalnız bu başvuru</option><option value="Company">Bu şirket</option>
          <option value="RoleGroup" disabled={!app.roleGroupId}>Bu rol grubu</option><option value="Default">Genel</option>
        </select></label>
        <label className="field">Bu cevabın geçerlilik sonu · isteğe bağlı<input type="date" value={expiry} onChange={e => { setExpiry(e.target.value); setConfirmedRevision(null); }} /></label>
        {profileField && <p>Ad, e-posta, maaş ve deneyim bilgilerini yukarıdaki profil ekranında doğrulayın.</p>}
        <label className="delete-check"><input type="checkbox" checked={reviewed} onChange={e => setConfirmedRevision(e.target.checked ? revision : null)} /> Cevabı ve kullanım kapsamını inceledim.</label>
        <button disabled={busy || !reviewed || !editable} type="submit">Cevabı kaydet ve devam et</button>
      </form>
      <form onSubmit={e => { e.preventDefault(); const label = newQuestion.trim(); if (!label) return; setConfirmedRevision(null);
        void perform('applications/questions', { applicationRef: app.draft.id, expectedRevision: revision, roleGroupId: app.roleGroupId,
          questions: [...app.draft.questions, { key: 'custom:' + label, label, language, maxLength: 2000, sensitive: manual }] }); }}>
        <label className="field">Yeni sorunun metni<input required maxLength={193} value={newQuestion} onChange={e => setNewQuestion(e.target.value)} /></label>
        <label className="field">Yeni sorunun dili<select value={language} onChange={e => setLanguage(e.target.value)}><option value="tr">Türkçe</option><option value="en">English</option></select></label>
        <label className="delete-check"><input type="checkbox" checked={manual} onChange={e => setManual(e.target.checked)} /> Hassas soru · otomatik cevaplanmasın.</label>
        <button className="secondary" disabled={busy || !app.sourcesCurrent} type="submit">Soruyu başvuruya ekle</button>
      </form>
      <DocumentAdaptation csrf={csrf} applicationRef={app.draft.id} workspaceRevision={revision}
        applicationPayloadHash={app.payloadHash} disabled={busy || !app.sourcesCurrent || cancelled} />
      <button className="secondary" disabled={busy} onClick={() => void perform('applications/' + app.draft.id + '/cancel', { expectedRevision: revision })}>Bu başvuruyu iptal et</button>
    </>}
  </article>;
}

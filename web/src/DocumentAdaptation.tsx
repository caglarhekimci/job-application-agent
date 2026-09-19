import { useEffect, useState } from 'react';

type Citation = { evidenceId: string; sourceSpan: string; experienceKind: string; exactText: string };
type Artifact = { content: string; contentHash: string; citations: Citation[] };
type Change = { kind: string; text: string; sourceSpan: string | null };
type Proposal = { bundleHash: string; resume: Artifact; coverLetter: Artifact; changes: Change[] };
type View = { revision: number; applicationRef: string; status: string; originalText: string;
  originalResumeHash: string; proposal: Proposal | null; approvedAt: string | null };

const kindNames: Record<string, string> = {
  Professional: 'Profesyonel iş', Internship: 'Staj', PartTime: 'Yarı zamanlı iş', PersonalProject: 'Kişisel proje'
};
const changeNames: Record<string, string> = {
  Included: 'Aynı sırada kullanıldı', Moved: 'İlana göre öne taşındı', Omitted: 'Öneriye alınmadı', Structural: 'Sabit başlık eklendi'
};

export function DocumentAdaptation({ csrf, applicationRef, workspaceRevision, applicationPayloadHash,
  disabled }: { csrf: string; applicationRef: string; workspaceRevision: number;
    applicationPayloadHash: string; disabled: boolean }) {
  const [view, setView] = useState<View | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [confirmedKey, setConfirmedKey] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    setConfirmedKey(null);
    void fetch(`/api/workspace/applications/${applicationRef}/document-adaptation`).then(async response => {
      if (!response.ok) throw new Error('Belge uyarlaması açılamadı.');
      const next: View = await response.json();
      if (active) setView(next);
    }).catch(reason => { if (active) setError((reason as Error).message); });
    return () => { active = false; };
  }, [applicationRef, workspaceRevision]);

  async function act(action: 'propose' | 'approve') {
    const proposal = view?.proposal;
    if (!view || action === 'approve' && !proposal) return;
    setBusy(true); setError(''); setConfirmedKey(null);
    try {
      const body = action === 'propose'
        ? { expectedRevision: view.revision, expectedApplicationPayloadHash: applicationPayloadHash }
        : { expectedRevision: view.revision, expectedBundleHash: proposal!.bundleHash };
      const response = await fetch(`/api/workspace/applications/${applicationRef}/document-adaptation/${action}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'X-JobAgent-Csrf': csrf }, body: JSON.stringify(body)
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({ error: 'LocalOperationFailed' }));
        throw new Error(problem.error === 'DocumentAdaptationStale'
          ? 'CV, profil veya ilan değişti. Yeni bir öneri oluşturun.'
          : 'Belge işlemi tamamlanamadı. Güncel kaynaklarla yeniden deneyin.');
      }
      setView(await response.json());
    } catch (reason) { setError((reason as Error).message); }
    finally { setBusy(false); }
  }

  const proposal = view?.proposal;
  const confirmationKey = proposal && view ? `${view.revision}:${proposal.bundleHash}` : '';
  const confirmed = confirmedKey === confirmationKey;
  return <section data-testid="document-adaptation">
    <h5>CV ve ön yazı uyarlaması</h5>
    <p className="quiet">Bu yerel öneri yalnız doğruladığınız CV cümlelerini seçip sıralar. Yeni iddia veya yapay zekâ üslup değişikliği eklemez.</p>
    {error && <p role="alert" className="error">{error}</p>}
    {!proposal && <button className="secondary" disabled={busy || disabled || !view}
      onClick={() => void act('propose')}>CV ve ön yazı önerisi oluştur</button>}
    {proposal && <>
      {view.status === 'Stale' && <p role="status">Kaynaklar değişti; bu öneri artık onaylanamaz veya indirilemez.</p>}
      <div className="form-grid">
        <div><h6>Kaynak CV</h6><pre className="source-text">{view.originalText}</pre></div>
        <div><h6>Önerilen CV</h6><pre className="source-text">{proposal.resume.content}</pre></div>
      </div>
      <details><summary>Ön yazı ve kaynakları</summary><pre className="source-text">{proposal.coverLetter.content}</pre>
        {proposal.coverLetter.citations.map(citation => <p key={citation.evidenceId}>
          <strong>{kindNames[citation.experienceKind] || citation.experienceKind}</strong> · {citation.sourceSpan}<br />{citation.exactText}
        </p>)}</details>
      <details><summary>Değişiklik karşılaştırması</summary>{proposal.changes.map((change, index) =>
        <p key={`${change.sourceSpan || 'structure'}-${index}`}><strong>{changeNames[change.kind] || change.kind}</strong>
          {change.sourceSpan ? ` · ${change.sourceSpan}` : ''}<br />{change.text}</p>)}</details>
      <button className="secondary" disabled={busy || disabled} onClick={() => void act('propose')}>Öneriyi yeniden oluştur</button>
      {view.status !== 'Approved' && view.status !== 'Stale' && <>
        <label className="delete-check"><input type="checkbox" checked={confirmed}
          onChange={event => setConfirmedKey(event.target.checked ? confirmationKey : null)} /> Bu belge içeriklerini ve kaynaklarını inceledim.</label>
        <button disabled={busy || disabled || !confirmed} onClick={() => void act('approve')}>Bu belge paketini onayla</button>
      </>}
      {view.status === 'Approved' && <><p className="pill green">Belge paketi onaylandı</p>
        <a className="download-link" download href={`/api/workspace/applications/${applicationRef}/document-adaptation/export/resume/${proposal.bundleHash}`}>Uyarlanmış CV’yi indir</a>
        <a className="download-link" download href={`/api/workspace/applications/${applicationRef}/document-adaptation/export/cover-letter/${proposal.bundleHash}`}>Ön yazıyı indir</a></>}
    </>}
  </section>;
}

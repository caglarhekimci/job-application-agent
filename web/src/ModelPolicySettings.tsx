import { useEffect, useState, type FormEvent } from 'react';

type ProviderMode = 'Fixture' | 'HostMediated' | 'Api';
type ApplicationUsage = { applicationRef: string; usedOperations: number; maximumOperations: number; remainingOperations: number };
type PolicyView = { revision: number; mode: ProviderMode; paidApiEnabled: boolean;
  maxAnswerProposalOperationsPerApplication: number; applications: ApplicationUsage[] };

export function ModelPolicySettings({ csrf, workspaceRevision, onChanged }: {
  csrf: string; workspaceRevision: number; onChanged: () => Promise<void>;
}) {
  const [policy, setPolicy] = useState<PolicyView | null>(null);
  const [mode, setMode] = useState<ProviderMode>('HostMediated');
  const [maximum, setMaximum] = useState(4);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (!csrf) return;
    const controller = new AbortController();
    void fetch('/api/workspace/model-policy', { signal: controller.signal }).then(async response => {
      if (!response.ok) throw new Error('Yerel öneri politikası okunamadı.');
      const current: PolicyView = await response.json();
      setPolicy(current); setMode(current.mode); setMaximum(current.maxAnswerProposalOperationsPerApplication);
    }).catch(reason => { if ((reason as Error).name !== 'AbortError') setError((reason as Error).message); });
    return () => controller.abort();
  }, [csrf, workspaceRevision]);

  async function save(event: FormEvent) {
    event.preventDefault();
    if (!policy) return;
    setBusy(true); setMessage(''); setError('');
    try {
      const response = await fetch('/api/workspace/model-policy', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-JobAgent-Csrf': csrf },
        body: JSON.stringify({ expectedRevision: policy.revision, mode,
          maxAnswerProposalOperationsPerApplication: maximum })
      });
      if (!response.ok) {
        const body = await response.json().catch(() => ({ error: 'LocalOperationFailed' }));
        throw new Error('Politika kaydedilemedi. (' + body.error + ')');
      }
      const saved: PolicyView = await response.json();
      setPolicy(saved); setMode(saved.mode); setMaximum(saved.maxAnswerProposalOperationsPerApplication);
      setMessage('Yerel öneri politikası kaydedildi.');
      await onChanged();
    } catch (reason) { setError((reason as Error).message); }
    finally { setBusy(false); }
  }

  return <section className="panel" data-testid="model-policy-settings">
    <p className="eyebrow">YEREL ÖNERİ POLİTİKASI</p><h3>Model önerilerini sınırla</h3>
    <p>Bu ayar, bu çalışma alanına yazılan model önerisi işlemlerini sınırlar. Codex modelini, token kullanımını veya hesap kotasını sınırlamaz. Ücretli API her zaman kapalıdır ve başka bir sağlayıcıya geçiş yapılmaz.</p>
    <form onSubmit={save}>
      <div className="form-grid">
        <label className="field">Öneri sağlayıcısı<select aria-label="Öneri sağlayıcısı" value={mode}
          onChange={event => { setMode(event.target.value as ProviderMode); setMessage(''); }}>
          <option value="HostMediated">Yerel eşlikçi üzerinden öneriler</option>
          <option value="Fixture">Yalnız sabit test verisi</option>
          <option value="Api" disabled>Ücretli API · kapalı</option>
        </select></label>
        <label className="field">Başvuru başına öneri işlemi sınırı<input aria-label="Başvuru başına öneri işlemi sınırı"
          type="number" min="1" max="10" required value={maximum}
          onChange={event => { setMaximum(Number(event.target.value)); setMessage(''); }} /></label>
      </div>
      <p className="quiet">Yerel eşlikçi modu, yalnız inceleme bekleyen öneriler yazabilir. Sabit test verisi modu eşlikçi önerilerini durdurur; kendi elle yaptığınız incelemeler açık kalır. Sınırı değiştirmek önceki kullanımı sıfırlamaz.</p>
      {policy?.applications.map(item => <p className="quiet" key={item.applicationRef}>Başvuru {item.applicationRef.slice(0, 8)} · {item.usedOperations}/{item.maximumOperations} işlem kullanıldı · {item.remainingOperations} kaldı</p>)}
      <button type="submit" disabled={busy || !policy}>Yerel öneri politikasını kaydet</button>
      {message && <p role="status" className="pill green">{message}</p>}
      {error && <p role="alert" className="error">{error}</p>}
    </form>
  </section>;
}

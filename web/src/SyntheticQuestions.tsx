import { useState } from 'react';
import type { FormEvent } from 'react';

export type SyntheticQuestionResolution = {
  key: string;
  label: string;
  language: string;
  maxLength: number | null;
  sensitive: boolean;
  requiresCandidateAttestation: boolean;
  status: string;
  value: string | null;
  reason: string;
};

export type SyntheticQuestionReview = {
  applicationRef: string;
  payloadHash: string;
  status: string;
  questions: SyntheticQuestionResolution[];
};

type ReviewedAnswer = {
  semanticKey: string;
  answer: string;
  scope: string;
  language: string;
  expiresAt?: string;
};

type Props = {
  review: SyntheticQuestionReview;
  busy: boolean;
  onReview: (body: { applicationRef: string; expectedPayloadHash: string; reviewedAnswers: ReviewedAnswer[] }) => Promise<void>;
};

const preferenceKeys = new Set([
  'preference.work.mode',
  'preference.travel',
  'preference.contact.method',
  'preference.contact.window'
]);

function displayLabel(key: string) {
  return key === 'preference.work.mode' ? 'Çalışma biçimi' :
    key === 'preference.travel' ? 'Seyahat tercihi' :
    key === 'preference.contact.method' ? 'İletişim tercihi' : 'Tercih edilen arama zamanı';
}

export function SyntheticQuestions({ review, busy, onReview }: Props) {
  const questions = review.questions.filter(question => preferenceKeys.has(question.key));
  const [answers, setAnswers] = useState<Record<string, string>>(() =>
    Object.fromEntries(questions.map(question => [question.key, question.value ?? ''])));
  const [scope, setScope] = useState('Application');
  const [expiresAt, setExpiresAt] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const terminal = ['Submitting', 'SubmittedVerified', 'SubmittedUnverified', 'Cancelled'].includes(review.status);
  const complete = questions.length > 0 && questions.every(question => Boolean(answers[question.key]?.trim()));

  function change(key: string, value: string) {
    setAnswers(previous => ({ ...previous, [key]: value }));
    setConfirmed(false);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!confirmed || !complete || terminal) return;
    const expiry = expiresAt ? new Date(expiresAt).toISOString() : undefined;
    await onReview({
      applicationRef: review.applicationRef,
      expectedPayloadHash: review.payloadHash,
      reviewedAnswers: questions.map(question => ({
        semanticKey: question.key,
        answer: answers[question.key],
        scope,
        language: question.language,
        ...(expiry ? { expiresAt: expiry } : {})
      }))
    });
  }

  if (questions.length === 0) return null;
  return <form className="answer-preview" onSubmit={event => void submit(event)}>
    <h4>Sentetik tercih soruları</h4>
    <p className="quiet">Eksik cevaplar gönderimden önce sizin incelemenizle, seçtiğiniz kapsamda kaydedilir.</p>
    <div className="form-grid">
      {questions.map(question => <div className="field" key={question.key}>
        <label htmlFor={'synthetic-' + question.key}>{displayLabel(question.key)}</label>
        {question.key === 'preference.work.mode' ? <select id={'synthetic-' + question.key} value={answers[question.key]} disabled={busy || terminal}
          onChange={event => change(question.key, event.target.value)}><option value="">Seçin</option><option value="remote">Uzaktan</option><option value="hybrid">Hibrit</option></select> :
        question.key === 'preference.travel' ? <select id={'synthetic-' + question.key} value={answers[question.key]} disabled={busy || terminal}
          onChange={event => change(question.key, event.target.value)}><option value="">Seçin</option><option value="false">Hayır</option><option value="true">Evet</option></select> :
        question.key === 'preference.contact.method' ? <select id={'synthetic-' + question.key} value={answers[question.key]} disabled={busy || terminal}
          onChange={event => change(question.key, event.target.value)}><option value="">Seçin</option><option value="email">E-posta</option><option value="phone">Telefon</option></select> :
        <input id={'synthetic-' + question.key} value={answers[question.key]} maxLength={question.maxLength ?? undefined} disabled={busy || terminal}
          onChange={event => change(question.key, event.target.value)} />}
        <small>{question.status === 'Resolved' ? 'Çözülmüş cevap' : 'İncelemeniz gerekiyor'} · {question.reason}</small>
      </div>)}
      <div className="field"><label htmlFor="synthetic-answer-scope">Cevap kapsamı</label>
        <select id="synthetic-answer-scope" value={scope} disabled={busy || terminal} onChange={event => { setScope(event.target.value); setConfirmed(false); }}>
          <option value="Application">Yalnız bu başvuru</option><option value="Company">Bu şirket</option>
          <option value="RoleGroup">.NET geliştirici rol grubu</option><option value="Default">Varsayılan</option>
        </select>
      </div>
      <div className="field"><label htmlFor="synthetic-answer-expiry">Geçerlilik sonu (isteğe bağlı)</label>
        <input id="synthetic-answer-expiry" type="datetime-local" value={expiresAt} disabled={busy || terminal}
          onChange={event => { setExpiresAt(event.target.value); setConfirmed(false); }} />
      </div>
    </div>
    {!terminal ? <div className="approval">
      <label className="delete-check"><input type="checkbox" checked={confirmed} disabled={busy || !complete}
        onChange={event => setConfirmed(event.target.checked)} />
        Bu cevapları, kapsamı ve geçerlilik süresini inceledim.</label>
      <button type="submit" disabled={busy || !complete || !confirmed}>İncelediğim cevapları bu kapsamda kaydet</button>
    </div> : null}
  </form>;
}

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Exam } from './types'
const schema = z.object({
  title: z.string().trim().min(1, 'Enter an exam title.').max(160),
  description: z.string().max(4000),
  durationMinutes: z
    .number('Enter a duration.')
    .int()
    .min(1, 'Duration must be at least 1 minute.')
    .max(240, 'Duration cannot exceed 240 minutes.'),
  passingPercentage: z
    .number('Enter a passing percentage.')
    .min(0)
    .max(100, 'Passing percentage cannot exceed 100.')
    .refine((n) => Math.round(n * 100) / 100 === n, 'Use up to two decimal places.'),
})
export type ExamValues = z.infer<typeof schema>
export function ExamForm({
  exam,
  submit,
}: {
  exam?: Exam
  submit: (values: ExamValues) => Promise<void>
}) {
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ExamValues>({
    resolver: zodResolver(schema),
    defaultValues: exam ?? {
      title: '',
      description: '',
      durationMinutes: 30,
      passingPercentage: 60,
    },
  })
  const locked = !!exam && exam.status !== 'Draft'
  return (
    <form
      noValidate
      onSubmit={handleSubmit(async (values) => {
        setError('')
        setSaved(false)
        try {
          await submit(values)
          setSaved(true)
        } catch (e) {
          setError((e as Error).message)
        }
      })}
    >
      <fieldset disabled={locked || isSubmitting} className="form-fieldset">
        <div className="field">
          <label htmlFor="exam-title">Exam title</label>
          <input
            id="exam-title"
            {...register('title')}
            placeholder="e.g. Web Platform Foundations"
            aria-invalid={!!errors.title}
          />
          {errors.title && <p className="field-error">{errors.title.message}</p>}
        </div>
        <div className="field">
          <label htmlFor="exam-description">Description & instructions</label>
          <textarea
            id="exam-description"
            {...register('description')}
            placeholder="Tell students what this exam covers and how to prepare."
          />
          {errors.description && <p className="field-error">{errors.description.message}</p>}
        </div>
        <div className="form-row">
          <div className="field">
            <label htmlFor="duration">Duration (minutes)</label>
            <input
              id="duration"
              type="number"
              min="1"
              max="240"
              {...register('durationMinutes', { valueAsNumber: true })}
              aria-invalid={!!errors.durationMinutes}
            />
            {errors.durationMinutes && (
              <p className="field-error">{errors.durationMinutes.message}</p>
            )}
          </div>
          <div className="field">
            <label htmlFor="passing">Passing percentage</label>
            <input
              id="passing"
              type="number"
              min="0"
              max="100"
              step="0.01"
              {...register('passingPercentage', { valueAsNumber: true })}
              aria-invalid={!!errors.passingPercentage}
            />
            {errors.passingPercentage && (
              <p className="field-error">{errors.passingPercentage.message}</p>
            )}
          </div>
        </div>
        {!locked && (
          <button className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : exam ? 'Save details' : 'Create exam draft'}
          </button>
        )}
      </fieldset>
      {error && (
        <p role="alert" className="notice error">
          {error}
        </p>
      )}
      {saved && (
        <p role="status" className="save-message">
          Exam details saved.
        </p>
      )}
    </form>
  )
}

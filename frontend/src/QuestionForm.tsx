import { useState } from 'react'
import { useFieldArray, useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Plus, X } from 'lucide-react'
import type { Question } from './types'
const schema = z
  .object({
    text: z.string().trim().min(1, 'Enter question text.').max(3000),
    marks: z.number().int().min(1, 'Marks must be at least 1.').max(1000),
    choices: z
      .array(
        z.object({
          text: z.string().trim().min(1, 'Enter choice text.').max(1000),
          isCorrect: z.boolean(),
        }),
      )
      .min(2)
      .max(8),
  })
  .superRefine((v, ctx) => {
    if (v.choices.filter((c) => c.isCorrect).length !== 1)
      ctx.addIssue({
        code: 'custom',
        path: ['choices'],
        message: 'Select exactly one correct answer.',
      })
    if (new Set(v.choices.map((c) => c.text.toUpperCase())).size !== v.choices.length)
      ctx.addIssue({
        code: 'custom',
        path: ['choices'],
        message: 'Answer choices must be distinct.',
      })
  })
export type QuestionValues = z.infer<typeof schema>
export function QuestionForm({
  question,
  submit,
  cancel,
}: {
  question?: Question
  submit: (values: QuestionValues) => Promise<void>
  cancel: () => void
}) {
  const [error, setError] = useState('')
  const {
    register,
    control,
    setValue,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<QuestionValues>({
    resolver: zodResolver(schema),
    defaultValues: question
      ? {
          text: question.text,
          marks: question.marks,
          choices: question.choices.map((c) => ({ text: c.text, isCorrect: !!c.isCorrect })),
        }
      : {
          text: '',
          marks: 5,
          choices: [
            { text: '', isCorrect: false },
            { text: '', isCorrect: false },
          ],
        },
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'choices' })
  const choices = useWatch({ control, name: 'choices' })
  return (
    <form
      className="question-editor"
      noValidate
      onSubmit={handleSubmit(async (values) => {
        setError('')
        try {
          await submit(values)
        } catch (e) {
          setError((e as Error).message)
        }
      })}
    >
      <div className="section-head">
        <h3>{question ? 'Edit question' : 'Add a question'}</h3>
        <button
          type="button"
          className="icon-button"
          aria-label="Cancel question editing"
          onClick={cancel}
        >
          <X size={20} />
        </button>
      </div>
      <div className="field">
        <label htmlFor="question-text">Question text</label>
        <textarea id="question-text" {...register('text')} autoFocus aria-invalid={!!errors.text} />
        {errors.text && <p className="field-error">{errors.text.message}</p>}
      </div>
      <div className="field marks-field">
        <label htmlFor="question-marks">Marks</label>
        <input
          id="question-marks"
          type="number"
          min="1"
          max="1000"
          {...register('marks', { valueAsNumber: true })}
        />
        {errors.marks && <p className="field-error">{errors.marks.message}</p>}
      </div>
      <fieldset className="form-fieldset">
        <legend>Answer choices</legend>
        <p className="hint">
          Select the radio beside the correct answer. Include 2–8 distinct choices.
        </p>
        <div className="choice-editor-list">
          {fields.map((field, i) => (
            <div className="choice-editor" key={field.id}>
              <input
                type="radio"
                name="correct-answer"
                aria-label={`Choice ${i + 1} is correct`}
                checked={choices[i]?.isCorrect ?? false}
                onChange={() =>
                  fields.forEach((_, j) =>
                    setValue(`choices.${j}.isCorrect`, i === j, { shouldValidate: true }),
                  )
                }
              />
              <div>
                <label className="sr-only" htmlFor={`choice-${i}`}>
                  Choice {i + 1}
                </label>
                <input
                  id={`choice-${i}`}
                  {...register(`choices.${i}.text`)}
                  placeholder={`Choice ${i + 1}`}
                />
                {errors.choices?.[i]?.text && (
                  <p className="field-error">{errors.choices[i]?.text?.message}</p>
                )}
              </div>
              <button
                type="button"
                disabled={fields.length <= 2 || isSubmitting}
                className="icon-button"
                aria-label={`Remove choice ${i + 1}`}
                onClick={() => remove(i)}
              >
                <X size={16} />
              </button>
            </div>
          ))}
        </div>
        {errors.choices?.message && <p className="field-error">{errors.choices.message}</p>}
        {errors.choices?.root?.message && (
          <p className="field-error">{errors.choices.root.message}</p>
        )}
        <button
          type="button"
          className="text-link"
          disabled={fields.length >= 8 || isSubmitting}
          onClick={() => append({ text: '', isCorrect: false })}
        >
          <Plus size={16} />
          Add answer choice
        </button>
      </fieldset>
      {error && (
        <p className="notice error" role="alert">
          {error}
        </p>
      )}
      <div className="actions">
        <button className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Saving question…' : 'Save question'}
        </button>
        <button type="button" className="button secondary" onClick={cancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}

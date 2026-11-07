export interface User {
  id: string
  email: string
  displayName: string
  role: 'Instructor' | 'Student'
}
export interface Exam {
  id: string
  title: string
  description: string
  durationMinutes: number
  passingPercentage: number
  status: 'Draft' | 'Published' | 'Closed'
  revision: number
  createdAt: string
}
export interface Choice {
  id: string
  text: string
  position: number
  isCorrect?: boolean
}
export interface Question {
  id: string
  text: string
  marks: number
  position: number
  choices: Choice[]
  selectedChoiceId?: string | null
}
export interface Attempt {
  id: string
  examId: string
  examVersionId: string
  title: string
  description: string
  durationMinutes: number
  passingPercentage: number
  status: 'Active' | 'Submitted' | 'Expired'
  revision: number
  startedAt: string
  deadline: string
  completedAt: string | null
  serverNow: string
  questions: Question[]
}
export interface Result {
  attemptId: string
  earnedMarks: number
  totalMarks: number
  percentage: number
  passed: boolean
  correctCount: number
  incorrectCount: number
  unansweredCount: number
}
export interface History {
  id: string
  examId: string
  title: string
  versionNumber: number
  status: string
  startedAt: string
  deadline: string
  completedAt: string | null
  result: Result | null
}
export interface ScoreReport {
  attemptId: string
  examId: string
  title: string
  description: string
  versionNumber: number
  passingPercentage: number
  status: string
  startedAt: string
  deadline: string
  completedAt: string
  result: Result
  feedback: (Question & { correctChoiceId: string })[] | null
}
export interface ExamPerformance {
  id: string
  title: string
  status: string
  attempts: number
  completed: number
  averagePercentage: number
  passRate: number
}
export interface InstructorReport {
  totalExams: number
  publishedExams: number
  totalAttempts: number
  completedAttempts: number
  averagePercentage: number
  passRate: number
  distribution: { label: string; count: number }[]
  exams: ExamPerformance[]
  attempts: (History & { studentName: string })[] | null
}
export interface AvailableExam {
  examId: string
  versionId: string
  number: number
  title: string
  description: string
  durationMinutes: number
  passingPercentage: number
  questionCount: number
  totalMarks: number
  showFeedback: boolean
}

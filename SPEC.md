# LoanDocAI — Spec

## Problem

Lending operations teams manually read salary slips and bank statements to capture applicant income details, which is slow and error-prone. LoanDocAI extracts these fields automatically and answers questions about lending policy with citations.

## Scope (v1)

- Input: text-based PDFs (no scanned images, no OCR in v1), max 5 MB, one document per request.
- Document types: salary_slip, bank_statement.
- All test data is synthetic. No real borrower data is used anywhere.

## Fields

Common to both types:

- document_type: "salary_slip" | "bank_statement"
- applicant_name: string

Salary slip:

- employer_name: string
- pay_month: string, format YYYY-MM
- gross_salary: decimal, INR, 2 decimal places
- net_salary: decimal, INR, 2 decimal places

Bank statement:

- bank_name: string
- statement_from: date, format YYYY-MM-DD
- statement_to: date, format YYYY-MM-DD
- opening_balance: decimal, INR, 2 decimal places
- closing_balance: decimal, INR, 2 decimal places
- total_credits: decimal, INR, 2 decimal places

## Correctness rules (used by the evaluation harness)

- Strings: correct if equal after trimming, collapsing internal whitespace, and ignoring case.
- Amounts: correct if equal when rounded to 2 decimal places.
- Dates and months: correct if exactly equal in the formats above.
- A field the model omits or returns as null counts as incorrect.
- Metrics reported: accuracy per field, accuracy per document type, overall field accuracy.

## Ground-truth format

For each data/synthetic/pdfs/<id>.pdf there is data/synthetic/truth/<id>.json containing the fields above with snake_case keys.

## Non-goals (v1)

OCR, multi-page scanned documents, document authenticity checks, PAN/Aadhaar extraction, storing uploaded documents.

## Privacy

Uploaded documents are processed in memory and not persisted. Logs must never contain document text or extracted personal values.

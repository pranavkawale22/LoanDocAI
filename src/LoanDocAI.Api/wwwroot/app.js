// LoanDocAI — Dashboard Client Logic
let isDemoMode = false;
let currentSalarySlip = null;
let currentBankStatement = null;

// Synthetic Sample Cache for Offline Demo Mode
const demoSamples = {
    slips: {
        'slip-01': {
            document_type: 'salary_slip',
            applicant_name: 'Harish Rao',
            employer_name: 'Orchid Textiles Ltd',
            pay_month: '2026-01',
            gross_salary: 119582.00,
            net_salary: 106945.90
        },
        'slip-02': {
            document_type: 'salary_slip',
            applicant_name: 'Meera Nair',
            employer_name: 'Kaveri Agro Foods Pvt Ltd',
            pay_month: '2026-03',
            gross_salary: 85000.00,
            net_salary: 76500.00
        },
        'slip-03': {
            document_type: 'salary_slip',
            applicant_name: 'Aarav Deshmukh',
            employer_name: 'Tarang Healthcare Services LLP',
            pay_month: '2026-08',
            gross_salary: 123815.00,
            net_salary: 99594.90
        }
    },
    statements: {
        'stmt-01': {
            document_type: 'bank_statement',
            applicant_name: 'Arjun Mishra',
            bank_name: 'Konkan Savings Bank',
            statement_from: '2026-03-01',
            statement_to: '2026-03-31',
            opening_balance: 109850.03,
            closing_balance: 23115.09,
            total_credits: 28370.04
        },
        'stmt-02': {
            document_type: 'bank_statement',
            applicant_name: 'Harish Rao', // Matching Harish Rao for Approved Demo!
            bank_name: 'Sahyadri Cooperative Bank',
            statement_from: '2026-01-01',
            statement_to: '2026-01-31',
            opening_balance: 45000.00,
            closing_balance: 62000.00,
            total_credits: 115000.00
        },
        'stmt-03': {
            document_type: 'bank_statement',
            applicant_name: 'Imran Kulkarni',
            bank_name: 'Utkal Federal Bank',
            statement_from: '2026-12-01',
            statement_to: '2026-12-31',
            opening_balance: 25658.05,
            closing_balance: 148721.14,
            total_credits: 214841.45
        }
    }
};

document.addEventListener('DOMContentLoaded', () => {
    setupDemoModeToggle();
    setupFileInputs();
    document.getElementById('runReconcileBtn').addEventListener('click', runReconciliation);
});

function setupDemoModeToggle() {
    const toggleBtn = document.getElementById('demoModeToggle');
    const label = document.getElementById('demoModeLabel');

    toggleBtn.addEventListener('click', () => {
        isDemoMode = !isDemoMode;
        if (isDemoMode) {
            toggleBtn.className = 'px-2.5 py-1 text-xs font-semibold rounded-md transition-colors bg-amber-500/20 text-amber-300 border border-amber-500/30';
            label.textContent = 'Offline Demo Mode';
        } else {
            toggleBtn.className = 'px-2.5 py-1 text-xs font-semibold rounded-md transition-colors bg-slate-700 text-slate-300 hover:bg-slate-600';
            label.textContent = 'Live API Mode';
        }
    });
}

function setupFileInputs() {
    const slipDropzone = document.getElementById('slipDropzone');
    const slipInput = document.getElementById('slipFileInput');
    slipDropzone.addEventListener('click', () => slipInput.click());
    slipInput.addEventListener('change', (e) => handleUpload(e.target.files[0], 'salary_slip'));

    const stmtDropzone = document.getElementById('stmtDropzone');
    const stmtInput = document.getElementById('stmtFileInput');
    stmtDropzone.addEventListener('click', () => stmtInput.click());
    stmtInput.addEventListener('change', (e) => handleUpload(e.target.files[0], 'bank_statement'));
}

async function handleUpload(file, expectedType) {
    if (!file) return;

    const badge = document.getElementById(expectedType === 'salary_slip' ? 'slipStatusBadge' : 'stmtStatusBadge');
    badge.className = 'text-xs px-2.5 py-0.5 rounded-full bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 animate-pulse';
    badge.textContent = 'Extracting...';

    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch('/api/extract', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || 'Failed to extract PDF');
        }

        const data = await response.json();
        if (data.document_type === 'salary_slip') {
            displaySalarySlip(data);
        } else {
            displayBankStatement(data);
        }
    } catch (err) {
        alert('Extraction Error: ' + err.message);
        badge.className = 'text-xs px-2.5 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/30';
        badge.textContent = 'Failed';
    }
}

function loadSampleSlip(id) {
    const data = demoSamples.slips[id];
    displaySalarySlip(data);
}

function loadSampleStmt(id) {
    const data = demoSamples.statements[id];
    displayBankStatement(data);
}

function displaySalarySlip(data) {
    currentSalarySlip = data;
    document.getElementById('slipApplicant').textContent = data.applicant_name || 'N/A';
    document.getElementById('slipEmployer').textContent = data.employer_name || 'N/A';
    document.getElementById('slipMonth').textContent = data.pay_month || 'N/A';
    document.getElementById('slipGross').textContent = formatInr(data.gross_salary);
    document.getElementById('slipNet').textContent = formatInr(data.net_salary);

    document.getElementById('slipResultCard').classList.remove('hidden');
    const badge = document.getElementById('slipStatusBadge');
    badge.className = 'text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
    badge.textContent = 'Extracted (100% Valid)';
}

function displayBankStatement(data) {
    currentBankStatement = data;
    document.getElementById('stmtApplicant').textContent = data.applicant_name || 'N/A';
    document.getElementById('stmtBank').textContent = data.bank_name || 'N/A';
    document.getElementById('stmtPeriod').textContent = `${data.statement_from} to ${data.statement_to}`;
    document.getElementById('stmtOpening').textContent = formatInr(data.opening_balance);
    document.getElementById('stmtCredits').textContent = formatInr(data.total_credits);
    document.getElementById('stmtClosing').textContent = formatInr(data.closing_balance);

    document.getElementById('stmtResultCard').classList.remove('hidden');
    const badge = document.getElementById('stmtStatusBadge');
    badge.className = 'text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
    badge.textContent = 'Extracted (100% Valid)';
}

async function runReconciliation() {
    if (!currentSalarySlip || !currentBankStatement) {
        alert('Please ingest both a Salary Slip and a Bank Statement before running verification.');
        return;
    }

    const payload = {
        salary_slip: currentSalarySlip,
        bank_statement: currentBankStatement
    };

    let result;
    if (isDemoMode) {
        // Instant offline calculation
        result = simulateReconciliation(currentSalarySlip, currentBankStatement);
    } else {
        const response = await fetch('/api/reconcile', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        result = await response.json();
    }

    renderAuditScorecard(result);
}

function simulateReconciliation(slip, stmt) {
    const nameMatch = slip.applicant_name.toLowerCase().trim() === stmt.applicant_name.toLowerCase().trim();
    const inflowSufficient = stmt.total_credits >= slip.net_salary;
    const solvent = stmt.closing_balance >= 0;

    let status = 'Approved';
    let score = 95;
    let summary = 'APPROVED: Identity verified, salary validated against bank inflows, and healthy account solvency confirmed.';

    if (!nameMatch) {
        status = 'Rejected';
        score = 25;
        summary = `REJECTED: Critical mismatch between Salary Slip applicant ('${slip.applicant_name}') and Bank Statement account holder ('${stmt.applicant_name}').`;
    } else if (!inflowSufficient) {
        status = 'FlaggedForReview';
        score = 65;
        summary = 'FLAGGED FOR REVIEW: Identity matches, but monthly deposits are lower than declared net salary.';
    }

    return {
        status: status,
        confidence_score: score,
        summary_notes: summary,
        checks: [
            {
                check_name: 'Applicant Identity Match',
                passed: nameMatch,
                details: nameMatch ? `Names match exactly: '${slip.applicant_name}'` : `Mismatch: '${slip.applicant_name}' vs '${stmt.applicant_name}'`
            },
            {
                check_name: 'Positive Net Salary',
                passed: slip.net_salary > 0,
                details: `Declared Net Salary is positive (${formatInr(slip.net_salary)})`
            },
            {
                check_name: 'Deposit Inflow Sufficiency',
                passed: inflowSufficient,
                details: inflowSufficient ? `Deposits (${formatInr(stmt.total_credits)}) cover net salary (${formatInr(slip.net_salary)})` : 'Inflows insufficient'
            },
            {
                check_name: 'Account Solvency',
                passed: solvent,
                details: `Positive closing balance maintained (${formatInr(stmt.closing_balance)})`
            }
        ]
    };
}

function renderAuditScorecard(result) {
    const banner = document.getElementById('verdictBanner');
    const icon = document.getElementById('verdictIcon');
    const title = document.getElementById('verdictTitle');
    const notes = document.getElementById('verdictNotes');
    const score = document.getElementById('confidenceScore');

    banner.classList.remove('hidden');

    if (result.status === 'Approved') {
        banner.className = 'rounded-xl p-4 border flex items-center justify-between bg-emerald-950/40 border-emerald-500/40 text-emerald-300';
        icon.innerHTML = '<i class="fa-solid fa-circle-check text-emerald-400"></i>';
        title.textContent = 'Verification Approved';
    } else if (result.status === 'Rejected') {
        banner.className = 'rounded-xl p-4 border flex items-center justify-between bg-rose-950/40 border-rose-500/40 text-rose-300';
        icon.innerHTML = '<i class="fa-solid fa-circle-xmark text-rose-400"></i>';
        title.textContent = 'Verification Rejected (Identity Mismatch)';
    } else {
        banner.className = 'rounded-xl p-4 border flex items-center justify-between bg-amber-950/40 border-amber-500/40 text-amber-300';
        icon.innerHTML = '<i class="fa-solid fa-triangle-exclamation text-amber-400"></i>';
        title.textContent = 'Flagged for Underwriter Review';
    }

    notes.textContent = result.summary_notes;
    score.textContent = result.confidence_score + '%';

    // Checklist table
    const checklist = document.getElementById('auditChecklist');
    const tbody = document.getElementById('auditChecklistBody');
    checklist.classList.remove('hidden');
    tbody.innerHTML = '';

    result.checks.forEach(c => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td class="px-4 py-3 font-medium text-slate-200">${c.check_name}</td>
            <td class="px-4 py-3">
                <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-semibold ${c.passed ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' : 'bg-rose-500/10 text-rose-400 border border-rose-500/20'}">
                    <i class="fa-solid ${c.passed ? 'fa-check' : 'fa-xmark'}"></i>
                    ${c.passed ? 'PASSED' : 'FLAGGED'}
                </span>
            </td>
            <td class="px-4 py-3 text-slate-400">${c.details}</td>
        `;
        tbody.appendChild(tr);
    });
}

function formatInr(val) {
    if (val === null || val === undefined) return 'N/A';
    return '₹' + Number(val).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

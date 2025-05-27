$(document).ready(function () {
    loadUserRequests();
});

// 🔍 View Button Event
$(document).on('click', '.btn-view-request', function () {
    const requestId = $(this).data('id');
    if (!requestId) return;

    // Load request data and fill the modal
    loadRequestDetails(requestId);
});

function loadUserRequests() {
    $.get('/TravelExpense/GetUserRequests', function (data) {
        const table = $('#requestListTbl').DataTable({
            data: data,
            bDestroy: true,
            order: [[2, "desc"]],
            dom: 'Bfrtip',
            buttons: ['copy', 'csv', 'excel', 'pdf', 'print'],
            columns: [
                { data: 'TarNo' },
                { data: 'TripPurpose' },
                {
                    data: 'RequestDate',
                    render: function (data) {
                        const match = /\/Date\((\d+)\)\//.exec(data);
                        if (match) {
                            const timestamp = parseInt(match[1]);
                            const date = new Date(timestamp);
                            return date.toLocaleDateString('en-GB');
                        }
                        return 'Invalid Date';
                    }
                },
                {
                    data: 'EstimatedCost',
                    render: function (d) {
                        return parseFloat(d).toLocaleString('vi-VN');
                    }
                },
                {
                    data: null,
                    render: function (data) {
                        const bgColor = data.ColorCode || '#6c757d';
                        return `
                            <span class="badge" style="background-color: ${bgColor}; color: #fff; font-weight: 500;">
                                ${data.DisplayName || 'Unknown'}
                            </span>`;
                    }
                },
                {
                    data: "ID",
                    title: "Actions",
                    orderable: false,
                    render: function (data, type, row) {
                        const role = sessionStorage.getItem("currentUserRole");
                        const status = row.DisplayName;

                        const canEdit = role === "Requester" && status === "Requester Pending";

                        const viewBtn = `<button class="btn btn-sm btn-outline-info btn-view-request" data-id="${data}">
                            <i class="fa fa-eye"></i> View
                        </button>`;

                        const editBtn = canEdit
                            ? `<a href="/TravelExpense/Index/${data}" class="btn btn-sm btn-outline-primary ml-1">
                                <i class="fa fa-edit"></i> Edit
                               </a>`
                            : "";

                        return `${viewBtn} ${editBtn}`;
                    }
                }
            ]
        });
    });
}

function loadRequestDetails(requestId) {
    $.get(`/TravelExpense/GetRequestViewDetails?id=${requestId}`, function (data) {
        if (!data) {
            showToast("Failed to load request details", "danger");
            return;
        }

        // Fill modal with all info (including budget)
        fillViewModal(data);

        // ✅ Show the modal
        $('#viewRequestModal').modal('show');
    }).fail(function () {
        showToast("Server error while loading request details", "danger");
    });
}

function fillViewModal(data) {
    const role = sessionStorage.getItem("currentUserRole") || "";
    $('#viewRequestID').val(data.ID);
    // 🔹 Travel Info
    $('#viewTarNo').text(data.TarNo || "");
    $('#viewFromDate').text(formatJSONDate(data.FromDate));
    $('#viewToDate').text(formatJSONDate(data.ToDate));
    $('#viewTripDays').text(data.TripDays ?? "");
    $('#viewRequestDate').text(formatJSONDate(data.RequestDate));
    $('#viewTripPurpose').text(data.TripPurpose || "");

    // 🔹 Budget
    $('#viewBudgetName').text(data.Budget?.BudgetName || "");
    $('#viewBudgetAmount').text(formatNumber(data.Budget?.BudgetAmount || 0));
    $('#viewBudgetUsed').text(formatNumber(data.Budget?.BudgetUsed || 0));
    $('#viewBudgetRemaining').text(formatNumber(data.Budget?.BudgetRemaining || 0));

    // 🔹 Cost Details
    $('#viewCostAir').text(data.CostDetails?.CostAir ?? 0);
    $('#viewCostHotel').text(data.CostDetails?.CostHotel ?? 0);
    $('#viewCostMeal').text(data.CostDetails?.CostMeal ?? 0);
    $('#viewCostOther').text(data.CostDetails?.CostOther ?? 0);
    $('#viewExchangeRate').text(data.ExchangeRate ?? "");
    $('#viewEstimatedCost').text(formatNumber(data.EstimatedCost || 0));

    // 🔹 Employees
    $('#viewEmployeeList').empty();
    (data.Employees || []).forEach(emp => {
        const item = `<li class="list-group-item">${emp.Name} - ${emp.Position}</li>`;
        $('#viewEmployeeList').append(item);
    });

    // 🔹 Requester Signature
    $('#viewRequesterSign').text(data.RequesterSign || "");
    $('#viewRequesterDate').text(formatJSONDate(data.CreatedDate));

    // 🔹 Approval Sections
    resetApprovalSections();

    const approvals = data.Approvals || [];
    showApprovalSections(role, approvals, data.StatusID);
}

function resetApprovalSections() {
    $('#approvalActions').addClass('d-none');
    $('#hodSection').addClass('d-none');
    $('#fcSection').addClass('d-none');
    //$('#hodSection').hide();
    //$('#fcSection').hide();
}

function showApprovalSections(role, approvals, statusID) {
    const hod = approvals.find(a => a.ApprovalStep === 1);
    const fc = approvals.find(a => a.ApprovalStep === 2);

    resetApprovalSections();

    // Show HOD Section
    if (hod) {
        showHODSection(hod, statusID);
    } else {
        setFallbackHODSection();
    }

    // Show FC Section (always shown for consistency)
    showFCSection(fc, statusID);

    // Approve/Reject buttons (based on role + section still pending)
    if (role === "HOD" && statusID === 2) {
        $('#approvalActions').removeClass('d-none');
    } else if (role === "FC" && statusID === 5) {
        $('#approvalActions').removeClass('d-none');
    }
}

function showHODSection(hod, statusID) {
    $('#viewHODName').text(hod.ApproverName || "N/A");
    $('#viewHODEmail').text(hod.ApproverEmail || "N/A");
    $('#viewHODPosition').text(hod.ApproverPosition || "N/A");
    $('#viewHODSignature').text(hod.ApproverSignature || "N/A");
    $('#viewHODSignDate').text(formatJSONDate(hod.ApproverSignDate) || "N/A");

    let statusLabel = "Not Reviewed";

    if (statusID === 2) {
        statusLabel = "Pending";
    } else if (statusID === 3) {
        statusLabel = "Approved";
    } else if (statusID === 4) {
        statusLabel = "Rejected";
    } else if (statusID > 4) {
        statusLabel = "Approved";
    }

    setApprovalStatusBadge("#hodApprovalStatus", statusLabel);
    $('#hodSection').removeClass('d-none');
}

function setFallbackHODSection() {
    $('#hodSection').removeClass('d-none');
    $('#viewHODName').text("N/A");
    $('#viewHODEmail').text("N/A");
    $('#viewHODPosition').text("N/A");
    $('#viewHODSignature').text("N/A");
    $('#viewHODSignDate').text("N/A");
    setApprovalStatusBadge('#hodApprovalStatus', 'Not Assigned');
}

function showFCSection(fc, statusID) {
    $('#viewFCName').text(fc?.ApproverName || "N/A");
    $('#viewFCEmail').text(fc?.ApproverEmail || "N/A");
    $('#viewFCPosition').text(fc?.ApproverPosition || "N/A");
    $('#viewFCSignature').text(fc?.ApproverSignature || "N/A");
    $('#viewFCSignDate').text(formatJSONDate(fc?.ApproverSignDate) || "N/A");

    let statusLabel = "Not Assigned";

    if (statusID === 5) {
        statusLabel = "Pending";
    } else if (statusID === 6) {
        statusLabel = "Approved";
    } else if (statusID === 7) {
        statusLabel = "Rejected";
    } else if (statusID > 7) {
        statusLabel = "Approved";
    } 

    setApprovalStatusBadge("#fcApprovalStatus", statusLabel);
    $('#fcSection').removeClass('d-none');
}

function setApprovalStatusBadge(selector, status) {
    let badgeClass = 'badge-secondary';
    let label = 'Not Assigned';
    let backgroundColor = '#6c757d'; // Default: Not Assigned (secondary)

    switch (status) {
        case 'Approved':
            badgeClass = 'badge-success';
            label = 'Approved';
            backgroundColor = '#28a745';
            break;
        case 'Pending':
            badgeClass = 'badge-warning';
            label = 'Pending';
            backgroundColor = '#ffc107';
            break;
        case 'Rejected':
            badgeClass = 'badge-danger';
            label = 'Rejected';
            backgroundColor = '#dc3545';
            break;
        case 'Not Reviewed':
            badgeClass = 'badge-info';
            label = 'Not Reviewed';
            backgroundColor = '#17a2b8';
            break;
        case 'Not Assigned':
        default:
            // Keep default styles
            label = 'Not Assigned';
            break;
    }

    $(selector)
        .removeClass()
        .addClass(`badge ${badgeClass}`)
        .css({
            backgroundColor: backgroundColor,
            color: 'white'
        })
        .text(label);
}

let pendingApprovalAction = null;

$('#approveBtn').click(() => {
    pendingApprovalAction = true;
    $('#confirmApprovalMessage').text("Are you sure you want to APPROVE this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#rejectBtn').click(() => {
    pendingApprovalAction = false;
    $('#confirmApprovalMessage').text("Are you sure you want to REJECT this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#confirmApprovalBtn').click(() => {
    $('#confirmApprovalModal').modal('hide');
    submitApproval(pendingApprovalAction);
});

function submitApproval(isApprove) {   
        const role = sessionStorage.getItem("currentUserRole") || "";
        const requestId = $('#viewRequestID').val();

        if (!requestId) {
            showToast("Missing Request ID", "danger");
            return;
        }

        const payload = {
            requestId: parseInt(requestId),
            isApprove: isApprove
        };

        const url = role === "HOD"
            ? '/TravelExpense/ApproveByHOD'
            : role === "FC"
                ? '/TravelExpense/ApproveByFC'
                : null;

        if (!url) {
            showToast("Unauthorized action for current role", "warning");
            return;
        }

    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (res) {
            if (res.success) {
                showToast("Approval status updated successfully!", "success");

                // Refresh modal contents in real-time
                loadRequestDetails(payload.requestId);
            } else {
                showToast(res.message || "Failed to update approval", "danger");
            }
        },
        error: function () {
            showToast("Server error while approving request", "danger");
        }
    });
}

$('#closeViewModalBtn').click(function () {
    $('#viewRequestModal').modal('hide'); // Close the modal
    loadUserRequests(); // Refresh the request list
});

$('#exportPDFBtn').click(function () {
    // 🔹 Clone the modal content
    const modal = document.querySelector('#viewRequestModal .modal-content');
    const cloned = modal.cloneNode(true);

    // 🔹 Remove footer
    const footer = cloned.querySelector('.modal-footer');
    if (footer) footer.remove();

    // 🔹 Get TAR No text from the live DOM
    const tarText = document.querySelector('#viewTarNo')?.textContent?.trim() || "TAR_No";

    // 🔹 Create a standalone header
    const tarOnly = document.createElement('h4');
    tarOnly.innerText = `TAR No: ${tarText}`;
    tarOnly.style.margin = "0 0 10px 0";
    tarOnly.style.fontWeight = "bold";

    // 🔹 Extract and prepare body content
    const body = cloned.querySelector('.modal-body');
    if (!body) {
        alert("No modal body found to export.");
        return;
    }

    // 🔹 Insert page break before FC Section (optional for multi-page layout)
    const fcSection = body.querySelector('#fcSection');
    if (fcSection) {
        const pageBreak = document.createElement('div');
        pageBreak.className = 'page-break';
        fcSection.before(pageBreak);
    }

    // 🔹 Combine into one wrapper
    const pdfElement = document.createElement('div');
    pdfElement.style.padding = '10px';
    pdfElement.appendChild(tarOnly);
    pdfElement.appendChild(body);

    // 🔹 Format filename with timestamp
    const now = new Date();
    const timestamp = now.toISOString().replace(/[:.-]/g, '_').slice(0, 16);
    const filename = `${tarText.replace(/\s+/g, '_')}_${timestamp}.pdf`;

    // 🔹 Export with html2pdf
    html2pdf()
        .set({
            margin: [0.5, 0.5, 0.5, 0.5],
            filename: filename,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, scrollY: 0 },
            jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait', putOnlyUsedFonts: true },
            pagebreak: { mode: ['css', 'legacy'] }
        })
        .from(pdfElement)
        .toPdf()
        .get('pdf')
        .then(pdf => {
            const totalPages = pdf.internal.getNumberOfPages();
            for (let i = 1; i <= totalPages; i++) {
                pdf.setPage(i);
                pdf.setFontSize(10);
                pdf.text(`Page ${i} of ${totalPages}`, 0.5, 11);
            }
        })
        .save();
});

function formatJSONDate(jsonDate) {
    if (!jsonDate) return "N/A";

    let timestamp = parseInt(jsonDate.replace(/[^0-9]/g, ""), 10);
    let date = new Date(timestamp);

    let day = String(date.getDate()).padStart(2, "0");
    let month = String(date.getMonth() + 1).padStart(2, "0"); // Months are 0-based
    let year = date.getFullYear();
    let hours = String(date.getHours()).padStart(2, "0");
    let minutes = String(date.getMinutes()).padStart(2, "0");

    return `${year}-${month}-${day} ${hours}:${minutes}`;
}

function formatNumber(num) {
    return num.toLocaleString('de-DE'); // e.g. 1.234.567
}
function showToast(message, type = "success") {
    var toastEl = $("#toastMessage");

    let iconColor;
    let toastTitle;

    switch (type) {
        case "success":
            iconColor = "green";
            toastTitle = "Success";
            break;
        case "danger":
            iconColor = "red";
            toastTitle = "Error";
            break;
        case "info":
            iconColor = "blue";
            toastTitle = "Info";
            break;
        case "warning":
            iconColor = "orange";
            toastTitle = "Warning";
            break;
        default:
            iconColor = "gray";
            toastTitle = "Notification";
    }

    $("#toastIcon").css("background-color", iconColor);
    $("#toastTitle").text(toastTitle);
    $("#toastTime").text("Just now");
    $("#toastBody").text(message);

    toastEl.toast('show');
}
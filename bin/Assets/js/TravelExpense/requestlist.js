
const StatusEnum = {
    RequesterPending: 1,
    RequesterCancelled: 2,
    HODPending: 3,
    HODApproved: 4,
    HODRejected: 5,
    GLPending: 6,
    GLApproved: 7,
    FCPending: 8,
    FCApproved: 9,
    FCRejected: 10,
    Done: 11
};

const LabelStatusEnum = {
    Pending: 'Pending',
    Approved: 'Approved',
    Rejected: 'Rejected',
    NotAssigned: 'Not Assigned',
    NotReviewed: 'Not Reviewed'
}

const RoleEnum = {
    Requester: 'Requester',
    HOD: 'HOD',
    GL: 'GL',
    FC: 'FC',
}

const StepEnum = {
    HOD: 1,
    GL: 2,
    FC: 3
}

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
                    data: null,
                    title: "Actions",
                    orderable: false,
                    render: function (data, type, row) {
                        const viewBtn = `<a class="btn btn-sm btn-outline-info btn-view-request" data-id="${data.ID}">
                            <i class="fa fa-eye"></i> View
                        </a>`;

                        const editBtn = data.EditMode
                            ? `<a href="/TravelExpense/Index/${data.ID}" class="btn btn-sm btn-outline-primary ml-1">
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
    $('#viewBudgetAmount').text(formatNumber(data.BudgetAmountAtSubmit || 0));
    $('#viewBudgetUsed').text(formatNumber(formatNumber(data.BudgetUsedAtSubmit) || 0));
    $('#viewBudgetRemaining').text(formatNumber(data.BudgetRemainingAtSubmit || 0));

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

    // Attachment File
    $('#ddAttachment').empty();
    (data.AttachmentFiles || []).forEach(name => {
        const file = `<li><a  style="color: #007bff" class="dropdown-item" href="/Upload/${name}" target="_blank">${name}</a></li>`
        $('#ddAttachment').append(file);
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
    $('#cancelActions').addClass('d-none');
}

function showApprovalSections(role, approvals, statusID) {
    const hod = approvals.find(a => a.ApprovalStep === StepEnum.HOD);
    const gl = approvals.find(a => a.ApprovalStep === StepEnum.GL);
    const fc = approvals.find(a => a.ApprovalStep === StepEnum.FC);

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
    if (role === RoleEnum.HOD && statusID === StatusEnum.HODPending) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').show();
    } else if (role === RoleEnum.GL && statusID === StatusEnum.GLPending) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').hide();
    } else if (role === RoleEnum.FC && statusID === StatusEnum.FCPending) {
        $('#approvalActions').removeClass('d-none');
        $('#rejectBtn').show();
    }

    //Cancel buttons
    if (role === RoleEnum.Requester && statusID <= StatusEnum.HODPending) {
        $('#cancelActions').removeClass('d-none');
    } else {
        $('#cancelActions').addClass('d-none');
    }
}

function showHODSection(hod, statusID) {
    $('#viewHODName').text(hod.ApproverName || "N/A");
    $('#viewHODEmail').text(hod.ApproverEmail || "N/A");
    $('#viewHODPosition').text(hod.ApproverPosition || "N/A");
    $('#viewHODSignature').text(hod.ApproverSignature || "N/A");
    $('#viewHODSignDate').text(formatJSONDate(hod.ApproverSignDate) || "N/A");

    let statusLabel = LabelStatusEnum.NotReviewed;

    if (statusID === StatusEnum.HODPending) {
        statusLabel = LabelStatusEnum.Pending;
    } else if (statusID === StatusEnum.HODApproved) {
        statusLabel = LabelStatusEnum.Approved;
    } else if (statusID === StatusEnum.HODRejected) {
        statusLabel = LabelStatusEnum.Rejected;
    } else if (statusID > StatusEnum.HODRejected) {
        statusLabel = LabelStatusEnum.Approved;
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
    setApprovalStatusBadge('#hodApprovalStatus', LabelStatusEnum.NotAssigned);
}

function showFCSection(fc, statusID) {
    $('#viewFCName').text(fc?.ApproverName || "N/A");
    $('#viewFCEmail').text(fc?.ApproverEmail || "N/A");
    $('#viewFCPosition').text(fc?.ApproverPosition || "N/A");
    $('#viewFCSignature').text(fc?.ApproverSignature || "N/A");
    $('#viewFCSignDate').text(formatJSONDate(fc?.ApproverSignDate) || "N/A");

    let statusLabel = LabelStatusEnum.NotAssigned;

    if (statusID === StatusEnum.FCPending) {
        statusLabel = LabelStatusEnum.Pending;
    } else if (statusID === StatusEnum.FCApproved) {
        statusLabel = LabelStatusEnum.Approved;
    } else if (statusID === StatusEnum.FCRejected) {
        statusLabel = LabelStatusEnum.Rejected;
    } else if (statusID > StatusEnum.FCRejected) {
        statusLabel = LabelStatusEnum.Approved;
    }

    setApprovalStatusBadge("#fcApprovalStatus", statusLabel);
    $('#fcSection').removeClass('d-none');
}

function setApprovalStatusBadge(selector, status) {
    let badgeClass = 'badge-secondary';
    let label = LabelStatusEnum.NotAssigned;
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
        case LabelStatusEnum.NotReviewed:
            badgeClass = 'badge-info';
            label = LabelStatusEnum.NotReviewed;
            backgroundColor = '#17a2b8';
            break;
        case LabelStatusEnum.NotAssigned:
        default:
            // Keep default styles
            label = LabelStatusEnum.NotAssigned;
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

let userAction = null;

$('#approveBtn').click(() => {
    userAction = 1;
    $('#confirmApprovalMessage').text("Are you sure you want to APPROVE this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#rejectBtn').click(() => {
    userAction = 0;
    $('#confirmApprovalMessage').text("Are you sure you want to REJECT this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#cancelBtn').click(() => {
    userAction = -1 ;
    $('#confirmApprovalMessage').text("Are you sure you want to CANCEL this request?");
    $('#confirmApprovalModal').modal('show');
});

$('#confirmApprovalBtn').click(() => {
    $('#confirmApprovalModal').modal('hide');
    submitApproval(userAction);
});


function submitApproval(userAction) {

    const role = sessionStorage.getItem("currentUserRole") || "";
    const requestId = $('#viewRequestID').val();

    if (!requestId) {
        showToast("Missing Request ID", "danger");
        return;
    }

    //Cancel TravelExpense Request
    if (userAction < 0) {
        $.ajax({
            url: '/TravelExpense/CancelByRequester',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                requestID: +requestId
            }) ,
            success: function (res) {
                if (res.success) {
                    showToast("Request cancelled successfully!", "success");

                    // Refresh modal contents in real-time
                    loadRequestDetails(requestId);
                } else {
                    showToast(res.message || "Failed to cancel request", "danger");
                }
            },
            error: function () {
                showToast("Server error while approving request", "danger");
            }
        });

    } else { //Approve or Reject TravelExpense Request
       

        const payload = {
            requestId: parseInt(requestId),
            isApprove: userAction == 0 ? false : true
        };


        let url;
        switch (role) {
            case RoleEnum.HOD:
                url = '/TravelExpense/ApproveByHOD';
                break;
            case RoleEnum.GL:
                url = '/TravelExpense/ApproveByGL';
                break;
            case RoleEnum.FC:
                url = '/TravelExpense/ApproveByFC';
                break;
            default:
                url = null;
        }

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
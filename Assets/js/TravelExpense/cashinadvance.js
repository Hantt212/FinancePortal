
$(document).ready(function () {
    // Trigger handler manually after DOM is ready
    $('#requiredCash').trigger('input'); // or 'change' depending on your handler
});
$('#approverCode').on('input', function () {
    const code = $(this).val().trim();
    if (!code || code.length < 5) {
        $('#approverInfoFields').hide();
        $('#approverName, #approverEmail, #approverPosition, #approverSign').val('');
        return;
    }

    $.ajax({
        url: '/TravelExpense/GetHodByCode',
        data: { code },
        success: function (res) {
            if (res.success) {
                $('#approverName').val(res.data.Name);
                $('#approverPosition').val(res.data.Position);
                $('#approverEmail').val(res.data.Email || '');
                $('#approverSign').val(res.data.Name?.toLowerCase() || '');
                $('#approverInfoFields').slideDown();
            } else {
                $('#approverInfoFields').hide();
                showToast(res.message || "Invalid approver code.", "danger");
            }
        },
        error: function () {
            $('#approverInfoFields').hide();
            showToast("Error retrieving approver info.", "danger");
        }
    });
});
$('#requiredCash').on('input', function () {
    const money = $(this).val().trim();
    if (money > 5000000) {
        $("#bankContainer").show();
    } else {
        $("#bankContainer").hide();
    }
});

//prevent enter text
$('#BAccountNo').on('input', function () {
    this.value = this.value.replace(/\D/g, ''); // removes non-digits
});

$("#submitCIAButton").click(function () {
    //Cash In Advance
    const ID = parseInt($('#ciaID').val()) || 0;
    const travelExpenseID = parseInt($('#travelID').val()) || 0;


    const reason = $('#reason').val().trim();
    const requiredCash = $('#requiredCash').val().trim();
    const requiredDate = $('#requiredDate').val().trim();
    const returnedDate = $('#returnedDate').val().trim();

    // Approval Info
    const hodId = $('#approverCode').val().trim();
    const hodName = $('#approverName').val().trim();
    const hodEmail = $('#approverEmail').val().trim();
    const hodPosition = $('#approverPosition').val().trim()

    const requesterSign = $('#requesterSign').val().trim();

    let beneficialName = "";
    let bankBranch = "";
    let accountNo = "";

    if (!reason) {
        showToast("Reason cannot be empty.", "warning");
        return;
    }
    if (!requiredCash) {
        showToast("Reason cannot be empty.", "warning");
        return;
    }
    if (!requiredDate) {
        showToast("Please enter Date Required By.", "warning");
        return;
    }
    if (!returnedDate) {
        showToast("Please enter Date/Month Returned By.", "warning");
        return;
    }
    if (requiredDate > returnedDate) {
        showToast("Required Date cannot be after Returned Date.", "warning");
        return;
    }
    if (!requesterSign) {
        showToast("Requester Signature cannot be empty.", "warning");
        return;
    }
    // Bank Info

    if (requiredCash > 5000000) {
        beneficialName = $('#BName').val().trim();
        bankBranch = $('#BBranch').val().trim();
        accountNo = $('#BAccountNo').val().trim();
        if (!beneficialName) {
            showToast("Beneficial Name cannot be empty.", "warning");
            return;
        }
        if (!bankBranch) {
            showToast("Bank Branch cannot be empty.", "warning");
            return;
        }
        if (!accountNo) {
            showToast("Account No cannot be empty.", "warning");
            return;
        }
    }


    const payload = {
        id: parseInt(ID),
        travelExpenseID,
        reason,
        requiredCash,
        requiredDate,
        returnedDate,
        beneficialName,
        bankBranch,
        accountNo,
        hodId,
        hodName,
        hodEmail,
        hodPosition,
        requesterSign
    };


    // 📤 Submit via AJAX
    $.ajax({
        url: '/CashInAdvance/SubmitForm',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (res) {
            if (res.success) {
                const msg = ID > 0 ? "Travel Expense updated successfully!" : "Travel Expense created successfully!";
                showToast(msg, "success");

                setTimeout(() => {
                    window.location.href = '/TravelExpense/List';
                }, 100);
            } else {
                showToast(res.message || "Submission failed.", "danger");
                //btn.prop('disabled', false).html('<i class="fa fa-save"></i> Submit');
            }

        },
        error: function () {
            //showToast("Server error during submission.", "danger");
            //btn.prop('disabled', false).html('<i class="fa fa-save"></i> Submit');
        }
    });

});


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

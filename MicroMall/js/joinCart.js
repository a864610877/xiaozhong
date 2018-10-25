﻿//加Num
function plusNum(el) {
    var num = $("#" + el).val();
    if (parseInt(num) <= 1)
        num = 1;
    $("#" + el).val(parseInt(num) + 1);
    $("#num").text(parseInt(num) + 1);
}

function reduceNum(el) {
    var num = $("#" + el).val();
    if (parseInt(num) <= 1) {
        $("#" + el).val(1);
        num = 1;
        return;
    }
    $("#num").text(parseInt(num) - 1);
    $("#" + el).val(parseInt(num) - 1);
}

var joinCart = function () {

    var Specification = "";
    var i = 0;
    var b = true;
    var txt = "";
    $(".gwcsx").each(function () {
        if ($(this).find("span").length > 0) {
            var s = "";
            var name = $(this).find("span").attr("data-name");

            $(this).find("span").each(function () {
                if ($(this).hasClass("checkbox")) {
                    s = name + "：" + $(this).html();
                    return;
                }
            });
            if (s == null || s == "") {
                txt = "请选择" + name;
                b = false;
                return false;
            }
            if (i == 0)
                Specification = s;
            else
                Specification += "," + s;
            i++;
        }
    });
    if (b == false) {
        alert(txt);
        return;
    }

    var obj = {
        commonid: $("#commonid").val(),
        num: $("#num").text(),
        spenName: Specification,
    }

    //alert("obj:"+JSON.stringify(obj));
    $.ajax({
        type: 'POST',
        url: "/JuMeiMall/AddSoppingCart",
        data: { "commodityId": obj.commonid, "quantity": obj.num, "Specification": obj.spenName },
        success: function (data) {
            if (data.Code == 110) {
                //$.openDialogUrl('未登陆', data.Msg);
                //$.openDialog("添加成功！");
                alert('未登陆');
                location.href = data.Msg;
            }
            else if (data.Code == 0) {
                alert('添加成功');
                $("#add_bgu").hide();
                $(".zhezhao").hide();
                //$.openDialog("添加成功！");
            } else {
                alert(data.Msg);
            }
            
            
            //检查购物车是否为空
            //checkShopCartIsNull();

            //setTimeout(function () { $.closeDialog(); }, 1000)
        },
        error: function () {
            alert("系统异常，请检查网络！");
        }
    });
}

$(function () {
    $(".aaaa").first().addClass("checkbox");
})

var checkspen = function (el) {
    if (el.attr("class") == "checkbox") {
        
    }
    else {
        el.addClass("checkbox");
        el.siblings().removeClass("checkbox");
    }
}
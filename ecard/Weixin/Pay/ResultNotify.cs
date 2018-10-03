﻿using Ecard;
using Ecard.Services;
using Moonlit.Data;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Linq;
using Ecard.Models;
using System.Threading;
using Ecard.SqlServices;
using Microsoft.Practices.Unity;

namespace WxPayAPI
{
    /// <summary>
    /// 支付结果通知回调处理类
    /// 负责接收微信支付后台发送的支付结果并对订单有效性进行验证，将验证结果反馈给微信支付后台
    /// </summary>
    public class ResultNotify : Notify
    {
        private Database _database;
        private DatabaseInstance _databaseInstance;
        //var database = new Database("ecard");
        //DatabaseInstance _databaseInstance = new DatabaseInstance(database);

        private readonly IAccountService IAccountService;
        private readonly ISiteService ISiteService;
        private readonly IOperationAmountLogsService IOperationAmountLogsService;
        private readonly ILog4netService ILog4netService;

        
        public IRebateService IRebateService { get; set; }
        public ResultNotify(Page page)
            : base(page)
        {
            _database = new Database("ecard");
            ILog4netService = new SqlLog4netService();
            ISiteService = new SqlSiteService(_database.OpenInstance());
            IAccountService = new SqlAccountService(_database.OpenInstance());
            IOperationAmountLogsService = new SqlOperationAmountLogsService(_database.OpenInstance(), IAccountService, ISiteService);
            IRebateService = new SqlRebateService(_database.OpenInstance(), IAccountService, ISiteService, IOperationAmountLogsService, ILog4netService);
        }

        public override void ProcessNotify()
        {

            WxPayData notifyData = GetNotifyData();

            //检查支付结果中transaction_id是否存在
            if (!notifyData.IsSet("transaction_id"))
            {
                //若transaction_id不存在，则立即返回结果给微信支付后台
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "支付结果中微信订单号不存在");
                Log.Error(this.GetType().ToString(), "The Pay result is error : " + res.ToXml());
                page.Response.Write(res.ToXml());
                page.Response.End();
            }
            string transaction_id = notifyData.GetValue("transaction_id").ToString();
            //查询订单，判断订单真实性
            if (!QueryOrder(transaction_id))
            {
                //若订单查询失败，则立即返回结果给微信支付后台
                WxPayData res = new WxPayData();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "订单查询失败");
                Log.Error(this.GetType().ToString(), "订单查询失败 : " + res.ToXml());
                page.Response.Write(res.ToXml());
                page.Response.End();
            }
            //查询订单成功
            else
            {
                WxPayData res = new WxPayData();
                try
                {
                    Log.Debug(this.GetType().ToString(), "订单状态 : " + notifyData.GetValue("result_code").ToString());
                    if (notifyData.GetValue("result_code").ToString() == "SUCCESS")
                    {
                        string orderNo = notifyData.GetValue("out_trade_no").ToString();
                        Log.Debug(this.GetType().ToString(), "订单号 : " + orderNo);
                        if (!string.IsNullOrWhiteSpace(orderNo))
                        {
                            using (_databaseInstance = new DatabaseInstance(_database))
                            {
                                var sql = "select * from fz_Orders where orderNo=@orderNo";
                                var item = new QueryObject<Ecard.Models.Order>(_databaseInstance, sql, new { orderNo = orderNo }).FirstOrDefault();
                                if (item != null)
                                {
                                    if (item.payState == Ecard.Models.PayStates.non_payment && item.orderState == Ecard.Models.OrderStates.awaitPay)
                                    {
                                        decimal amount = 0;
                                        if (int.Parse(notifyData.GetValue("total_fee").ToString()) > 0)
                                            amount = decimal.Parse(notifyData.GetValue("total_fee").ToString()) / 100;
                                        if (item.payAmount == amount)
                                        {
                                            _databaseInstance.BeginTransaction();
                                            item.payState = Ecard.Models.PayStates.paid;
                                            item.orderState = Ecard.Models.OrderStates.paid;
                                            item.payType = PayTypes.weChatPayment;
                                            item.submitTime = DateTime.Now;
                                            _databaseInstance.Update(item, "fz_Orders");


                                            //var sql2 = "select * from fz_Accounts where accountId=@accountId";
                                            //var item2 = new QueryObject<Ecard.Models.Account>(_databaseInstance, sql2, new { accountId = item.userId }).FirstOrDefault();
                                            //if (item2 !=null)
                                            //{
                                            //    if (!string.IsNullOrWhiteSpace(item2.openID))
                                            //    {
                                            //        var message = new Fz_Messages();
                                            //        message.accountId = item2.accountId;
                                            //        message.openId = item2.openID;
                                            //        message.state = MessagesState.staySend;
                                            //        message.submitTime = DateTime.Now;
                                            //        message.keyword1 = item.orderNo;
                                            //        message.keyword2 = "已付款";
                                            //        message.msgType = MsgType.orderState;
                                            //        _databaseInstance.Insert(message, "Fz_Messages"); 
                                            //    }
                                            //}
                                            _databaseInstance.Commit();
                                            Log.Debug(this.GetType().ToString(), "开始返利 : " + orderNo);
                                            if (item.orderType == OrderType.normal)
                                                IRebateService.Rebate2(item.orderId);
                                            Log.Debug(this.GetType().ToString(), "返利结束 : " + orderNo);
                                        }
                                        else
                                        {
                                            res.SetValue("return_code", "FAIL");
                                            res.SetValue("return_msg", "金额错误，支付金额于订单金额不一致");
                                            Log.Error(this.GetType().ToString(), "订单金额失败 : " + res.ToXml());
                                            page.Response.Write(res.ToXml());
                                            page.Response.End();
                                        }
                                    }
                                }

                            }
                        }
                    }

                    res.SetValue("return_code", "SUCCESS");
                    res.SetValue("return_msg", "OK");
                    Log.Info(this.GetType().ToString(), "order query success : " + res.ToXml());
                    page.Response.Write(res.ToXml());
                    page.Response.End();
                }

                catch (Exception ex)
                {
                    if (!(ex is System.Threading.ThreadAbortException))
                    {
                        res.SetValue("return_code", "FAIL");
                        res.SetValue("return_msg", "订单状态修改失败");
                        Log.Error(this.GetType().ToString(), "Order query failure : " + res.ToXml() + ex);
                        page.Response.Write(res.ToXml());
                        page.Response.End();
                    }

                }



            }

        }

        //查询订单
        private bool QueryOrder(string transaction_id)
        {
            WxPayData req = new WxPayData();
            req.SetValue("transaction_id", transaction_id);
            WxPayData res = WxPayApi.OrderQuery(req);
            if (res.GetValue("return_code").ToString() == "SUCCESS" &&
                res.GetValue("result_code").ToString() == "SUCCESS")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
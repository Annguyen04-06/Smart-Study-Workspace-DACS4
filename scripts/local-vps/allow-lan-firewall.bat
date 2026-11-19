@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - MO TUONG LUA CHO DIEN THOAI CUNG WI-FI (CHAY 1 LAN)
REM   Se hien hop thoai UAC xin quyen Admin dung 1 lan. Chi mo cong 5000/5500
REM   cho thiet bi trong cung mang con (LocalSubnet). Link HTTPS Cloudflare khong can buoc nay.
REM ===============================================================================
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0allow-lan-firewall.ps1"

# Tutorial NBomber — Non-functional / Quality Attribute Testing

Tutorial berbahasa Indonesia untuk memahami **load, stress, throughput, latency percentiles, dan error rate**. Masing-masing topik mempunyai **satu studi kasus**. Kita akan menguji API toko online lokal, membaca hasilnya, lalu memutuskan apakah kebutuhan kualitasnya terpenuhi.

**Anda tidak perlu menulis aplikasi dari nol.** API C#, program NBomber, dan GitHub Actions sudah disediakan. Semua kode lengkap juga dicantumkan di bagian akhir README agar materi dapat dipelajari tanpa berpindah file. Langkah menjalankan tersedia untuk **Linux, macOS, dan Windows**.

> Ikuti bagian 1–4 terlebih dahulu. Setelah itu, jalankan bagian 5–9 satu per satu. Jangan menjalankan beberapa load generator sekaligus pada API latihan yang sama.

## Daftar isi

1. [Apa yang dipelajari dan hubungannya dengan quality attributes](#1-apa-yang-dipelajari-dan-hubungannya-dengan-quality-attributes)
2. [Konsep dasar sebelum menjalankan test](#2-konsep-dasar-sebelum-menjalankan-test)
3. [Persiapan dari nol: Linux, macOS, Windows](#3-persiapan-dari-nol-linux-macos-windows)
4. [Menjalankan API dan mengenali hasil test](#4-menjalankan-api-dan-mengenali-hasil-test)
5. [Load — katalog pada jam sibuk](#5-load--katalog-pada-jam-sibuk)
6. [Stress — stok saat flash sale](#6-stress--stok-saat-flash-sale)
7. [Throughput — kapasitas layanan harga](#7-throughput--kapasitas-layanan-harga)
8. [Latency percentiles — pencarian produk](#8-latency-percentiles--pencarian-produk)
9. [Error rate — checkout ketika payment bermasalah](#9-error-rate--checkout-ketika-payment-bermasalah)
10. [Membaca report dan menulis kesimpulan](#10-membaca-report-dan-menulis-kesimpulan)
11. [GitHub Actions: membuktikan PASS dan FAIL di tiga OS](#11-github-actions-membuktikan-pass-dan-fail-di-tiga-os)
12. [Validitas eksperimen dan batas kesimpulan](#12-validitas-eksperimen-dan-batas-kesimpulan)
13. [Troubleshooting](#13-troubleshooting)
14. [Penjelasan kode C# dan kode lengkap](#14-penjelasan-kode-c-dan-kode-lengkap)
15. [Kode lengkap workflow dan pengujinya](#15-kode-lengkap-workflow-dan-pengujinya)
16. [Referensi resmi](#16-referensi-resmi)

## 1. Apa yang dipelajari dan hubungannya dengan quality attributes

### 1.1 Functional versus non-functional

Functional testing menanyakan **“apakah fungsinya benar?”** Contohnya, membuka katalog menghasilkan HTTP 200 dengan data produk yang benar.

Non-functional testing menanyakan **“seberapa baik fungsi tersebut bekerja pada kondisi tertentu?”** Katalog mungkin benar ketika dipanggil sekali, tetapi lambat atau sering gagal ketika banyak orang membukanya.

Quality attribute adalah sifat kualitas yang ingin dibuktikan melalui pengujian tersebut. Di tutorial ini, fokusnya sebagai berikut:

| Atribut kualitas | Pertanyaan | Bukti yang dicari |
| --- | --- | --- |
| Efisiensi performa: perilaku waktu | Seberapa cepat respons pada beban tertentu? | p50, p95, p99, rata-rata waktu respons |
| Efisiensi performa: kapasitas | Berapa banyak pekerjaan yang masih dapat dilayani dengan kualitas yang disepakati? | Successful RPS bersama latensi dan error rate |
| Keandalan dalam kondisi uji | Seberapa sering operasi gagal? | Jumlah gagal, persentase gagal, jenis status kegagalan |
| Perilaku saat overload | Apa yang terjadi ketika permintaan melampaui kapasitas? | Penolakan HTTP 503, penurunan successful RPS, perubahan latensi |
| Pemulihan setelah overload | Apakah kondisi membaik setelah beban dikurangi? | Kurva metrik pada fase terakhir stress test |

**Load dan stress adalah jenis/pola pengujian. Throughput, latency percentiles, dan error rate adalah metrik.** Metrik tersebut juga diamati pada load dan stress test. Masing-masing diberi satu studi kasus agar tujuan belajarnya jelas.

Pengujian performa tidak otomatis membuktikan semua atribut kualitas. Keamanan, aksesibilitas, kemudahan pemeliharaan, dan ketahanan berbulan-bulan membutuhkan metode lain. Pemakaian CPU/RAM juga memerlukan pemantauan server terpisah; report client saja belum membuktikan efisiensi resource.

### 1.2 Ubah “harus cepat” menjadi kebutuhan yang dapat diuji

Gunakan format berikut:

> Pada **lingkungan tertentu**, ketika **beban tertentu** diberikan ke **komponen tertentu**, sistem harus menunjukkan **respons terukur** selama **durasi tertentu**.

Contoh kebutuhan kualitas katalog:

| Unsur | Spesifikasi latihan |
| --- | --- |
| Sumber stimulus | Pengunjung katalog toko |
| Stimulus | Kedatangan 50 request per detik |
| Lingkungan | API lokal dalam mode Release, setelah warm-up |
| Komponen | `GET /api/catalog` |
| Respons | HTTP sukses dengan JSON `ok: true` |
| Ukuran respons | Successful RPS ≥ 49; p95 ≤ 200 ms; error rate < 1% |
| Durasi pengukuran | 120 detik pada fase stabil |

**SLI** adalah indikator yang diukur, misalnya p95. **SLO** adalah targetnya, misalnya p95 ≤ 200 ms. **SLA** adalah komitmen layanan yang umumnya mempunyai konsekuensi kontraktual. Target tutorial ini merupakan SLO latihan, bukan SLA dan bukan angka standar untuk semua aplikasi.

### 1.3 Rangkuman lima studi kasus

| Topik | Satu studi kasus | Yang ingin diputuskan |
| --- | --- | --- |
| Load | Katalog pada jam sibuk | Apakah beban normal memenuhi SLO? |
| Stress | Stok saat flash sale | Pada fase mana kualitas mulai rusak dan apakah pulih? |
| Throughput | Layanan harga produk | Berapa laju sukses yang dapat dipertahankan? |
| Latency percentiles | Pencarian produk | Apakah ekor distribusi terlalu lambat meskipun mayoritas cepat? |
| Error rate | Checkout saat payment bermasalah | Apakah kegagalan terdeteksi dan melanggar SLO? |

## 2. Konsep dasar sebelum menjalankan test

### 2.1 Request, scenario, dan iterasi

- **Request**: satu panggilan HTTP, misalnya membuka `/api/catalog`.
- **Scenario**: alur yang dikerjakan NBomber. Di tutorial ini setiap scenario hanya berisi satu request.
- **Iterasi**: satu kali menjalankan scenario tersebut.
- **Virtual user**: instance yang menjalankan scenario. Bukan jumlah manusia sungguhan.
- **RPS**: request per second, yaitu jumlah request per detik.
- **Warm-up**: pemanasan runtime/koneksi sebelum pengukuran utama.
- **Threshold/check**: batas yang dipakai untuk menentukan lulus/gagal.

Di semua kasus, **satu iterasi = satu HTTP request, tanpa retry**. Karena itu jumlah iterasi sukses bisa dibaca sebagai jumlah request sukses. Jika scenario kelak berisi login → cari → checkout, hubungan tersebut berubah: satu iterasi bisa mengirim tiga request.

NBomber menyediakan `Step.Run` untuk memecah alur menjadi operasi terukur. Kita belum membutuhkannya karena satu scenario hanya satu operasi. Lihat [dokumentasi Step](https://nbomber.com/docs/nbomber/scenario/step/).

### 2.2 Beban yang dikirim berbeda dari throughput yang berhasil

**Offered load** adalah laju pekerjaan yang dijadwalkan masuk. **Achieved throughput** adalah laju pekerjaan yang benar-benar selesai. Menyetel 300 iterasi/detik belum membuktikan server berhasil melayani 300 request/detik.

Contoh perhitungan untuk jendela pengamatan yang sama:

```text
Durasi T = 60 detik
Request sukses N_ok = 17.400
Request gagal N_fail = 600
Total N_all = 18.000

Successful throughput = N_ok / T = 290 request/detik
Failed throughput = N_fail / T = 10 request/detik
Completed throughput = N_all / T = 300 request/detik
Error rate = N_fail / N_all × 100% = 3,33%
```

300 request/detik selesai, tetapi hanya 290 yang sukses. Jika SLO error rate < 1%, kondisi ini gagal. Jangan menganggap HTTP 503 yang sangat cepat sebagai tanda server mampu melayani transaksi dengan baik.

Jika tidak ada sampel, error rate tidak dapat disimpulkan. Jangan menyatakan layanan sehat hanya karena “0 request gagal” ketika tidak ada request yang berhasil dijalankan.

### 2.3 Open model dan closed model

Tutorial menggunakan **open model**: pekerjaan baru dijadwalkan pada laju tertentu tanpa menunggu seluruh pekerjaan sebelumnya selesai.

```csharp
Simulation.Inject(rate: 50,
    interval: TimeSpan.FromSeconds(1),
    during: TimeSpan.FromSeconds(60))
```

Artinya 50 iterasi baru dijadwalkan setiap detik selama 60 detik. Karena satu iterasi berisi satu request, offered load-nya sekitar 50 request/detik.

Bandingkan dengan closed model:

```csharp
Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(60))
```

Ini mempertahankan 50 virtual user yang terus mengulang scenario. Masing-masing menunggu iterasinya selesai sebelum mengulang. **50 user bukan 50 RPS.** Tanpa think time, jika setiap iterasi memerlukan 0,2 detik, perkiraan kasarnya `50 / 0,2 = 250` iterasi/detik.

Endpoint latihan menerima request independen, sehingga open model sesuai dengan pertanyaan eksperimen. Perbedaan model dan API simulasinya dijelaskan di [Load Simulation](https://nbomber.com/docs/nbomber/load-simulation/).

### 2.4 Apa yang dimaksud latensi pada tutorial ini?

NBomber mengukur durasi iterasi yang berisi pengiriman HTTP, menunggu respons, membaca body, dan memvalidasi JSON. Hasilnya mencakup overhead client/jaringan. Ini bukan waktu query database saja dan bukan waktu render browser.

Waktu tunggu sebelum generator mulai menjalankan iterasi tidak otomatis sama dengan latensi yang diukur di dalam iterasi. Karena itu tetap periksa apakah generator mampu menghasilkan beban yang diminta.

## 3. Persiapan dari nol: Linux, macOS, Windows

### Langkah 1 — pasang .NET SDK 10

Buka [halaman unduh .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Pilih **SDK**, lalu pilih OS dan arsitektur komputer Anda. SDK diperlukan untuk build; menginstal runtime saja belum cukup.

- **Windows**: pilih installer SDK Windows. Gunakan Arm64 untuk perangkat ARM, x64 untuk PC Intel/AMD biasa.
- **macOS**: pilih installer SDK macOS. Apple Silicon menggunakan Arm64; Mac Intel menggunakan x64.
- **Linux**: ikuti petunjuk instalasi SDK untuk distribusi Anda pada halaman resmi.

Setelah instalasi selesai, tutup lalu buka kembali terminal agar PATH diperbarui.

| OS | Terminal yang dapat dipakai |
| --- | --- |
| Linux | Terminal bawaan distribusi |
| macOS | Terminal |
| Windows | PowerShell atau Windows Terminal dengan profil PowerShell |

Ketik perintah ini, lalu tekan Enter:

```text
dotnet --version
```

**Hasil yang diharapkan:** versi dimulai dengan `10.0.`. Angka patch di belakang boleh berbeda. Jika tertulis “command not found” atau “not recognized”, berhenti dulu dan perbaiki instalasi/PATH.

Untuk memastikan runtime web tersedia:

```text
dotnet --list-runtimes
```

Harus ada `Microsoft.NETCore.App 10.0...` dan `Microsoft.AspNetCore.App 10.0...`. Distribusi Linux tertentu memisahkan paket ASP.NET Core; pasang runtime web tersebut jika belum ada.

### Langkah 2 — ambil repository

Pilihan termudah jika belum menggunakan Git:

1. Buka [repository tutorial](https://github.com/Araryarch67/nbomber-tutorials).
2. Klik tombol **Code**.
3. Klik **Download ZIP**.
4. Ekstrak ZIP. Jangan menjalankan tutorial langsung dari dalam arsip ZIP.
5. Buka folder hasil ekstrak. Anda harus melihat `README.md`, `DemoApi`, dan `PerformanceTests`.

Jika Git sudah terpasang, gunakan alternatif berikut:

```text
git clone https://github.com/Araryarch67/nbomber-tutorials.git
cd nbomber-tutorials
```

### Langkah 3 — buka terminal di folder repository

- **Windows**: buka folder hasil ekstrak di File Explorer, klik kanan area kosong lalu pilih **Open in Terminal**. Jika memakai `cd`, beri tanda kutip pada path yang mengandung spasi.
- **macOS/Linux**: buka Terminal, ketik `cd `, lalu seret folder repository ke terminal atau ketik path-nya, dan tekan Enter.

Contoh berikut hanya menunjukkan bentuk perintah; sesuaikan path dengan lokasi Anda:

```text
cd "lokasi-folder-anda/nbomber-tutorials"
```

Tidak perlu mengetik simbol prompt seperti `$` atau `PS>`. Salin perintah di dalam blok kode saja. Jalankan baris demi baris, dari atas ke bawah.

### Langkah 4 — pulihkan paket dan build

Di folder yang berisi `README.md`, jalankan:

```text
dotnet restore DemoApi
dotnet restore PerformanceTests
dotnet build DemoApi -c Release --no-restore
dotnet build PerformanceTests -c Release --no-restore
```

**Hasil yang diharapkan:** kedua build menampilkan `Build succeeded` dan `0 Error(s)`. Restore pertama memerlukan internet karena mengunduh paket NuGet. `-c Release` memilih build yang dioptimalkan; `--no-restore` menghindari pengulangan restore.

Program menggunakan **NBomber 6.7.1** yang versinya ditetapkan di `.csproj`. Instalasi library melalui NuGet dijelaskan di [panduan instalasi NBomber](https://nbomber.com/docs/getting-started/installation/), dan paketnya tersedia di [NuGet versi 6.7.1](https://www.nuget.org/packages/NBomber/6.7.1).

Untuk mengikuti tutorial lokal, **Python tidak diperlukan**. Python hanya digunakan oleh pembungkus CI agar pengelolaan proses API sama di tiga OS. GitHub Actions memasangnya otomatis.

## 4. Menjalankan API dan mengenali hasil test

### Langkah 1 — siapkan dua terminal

Buka **dua terminal** yang sama-sama berada di folder repository:

- **Terminal A** menjalankan API. Biarkan hidup selama latihan.
- **Terminal B** menjalankan NBomber satu kasus setiap kali.

Jika Terminal B dibuka di folder berbeda, lakukan `cd` ke repository lagi. Setiap terminal mempunyai direktori kerjanya sendiri.

### Langkah 2 — nyalakan API di Terminal A

```text
dotnet run --project DemoApi -c Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5080
```

Perintah ini sama pada Linux, macOS, dan PowerShell Windows. `--no-launch-profile` menghindari port dari konfigurasi IDE. Tanda `--` memisahkan argumen `dotnet run` dari argumen aplikasi.

**Hasil yang diharapkan:** proses tetap berjalan dan terminal belum kembali ke prompt. API sengaja hanya mencetak log Warning ke atas; tidak muncul pesan “listening” tetap bisa normal.

### Langkah 3 — cek API lewat browser

Buka alamat berikut di browser:

```text
http://127.0.0.1:5080/health
```

Anda harus melihat JSON seperti:

```json
{"ok":true}
```

Lalu buka:

```text
http://127.0.0.1:5080/api/catalog
```

Hasilnya harus mempunyai `ok: true`, `productId`, dan nama `Kopi`. Jika browser tidak bisa terhubung, jangan lanjut test: periksa Terminal A dan port 5080.

### Langkah 4 — pahami target latihan

| Endpoint | Perilaku buatan | Digunakan untuk |
| --- | --- | --- |
| `GET /api/catalog` | Menunggu 30 ms, lalu sukses | Load |
| `GET /api/inventory` | Maksimal 20 pekerjaan berjalan, 200 ms per pekerjaan | Stress |
| `GET /api/prices` | Maksimal 20 pekerjaan berjalan, 40 ms per pekerjaan | Throughput |
| `GET /api/search` | Sekitar 90% menunggu 40 ms, 8% 250 ms, 2% 1.200 ms | Latency percentiles |
| `POST /api/checkout?failurePercent=10` | Menunggu 80 ms, peluang gagal HTTP 503 sebesar 10% | Error rate |

Inventory dan prices menunggu slot maksimal 500 ms. Jika seluruh slot tetap penuh, API mengembalikan HTTP 503. Checkout merupakan simulasi; tidak ada uang, order sungguhan, atau layanan payment eksternal.

### Langkah 5 — pahami tiga hasil program

Setelah run selesai, perhatikan bagian akhir output, bukan hanya gambar/animasi NBomber:

| Hasil | Makna | Exit code |
| --- | --- | --- |
| `SLO PASS` | Sampel memadai dan seluruh check kasus terpenuhi | `0` |
| `SLO FAIL` | Sampel valid, tetapi setidaknya satu check kualitas gagal | `1` |
| `INVALID` / prasyarat bermasalah | Sampel tidak cukup, jenis error tidak sesuai latihan, API mati, atau argumen salah | `2` untuk kondisi yang ditangani program |

Exception tak terduga juga bisa membuat proses berhenti; selalu baca log dan `summary.json`. Exit nonzero saja belum membuktikan ada pelanggaran SLO yang valid.

Setelah setiap perintah, cek exit code sebelum mengetik perintah lain:

**Linux/macOS (Bash/Zsh):**

```bash
echo $?
```

**Windows PowerShell:**

```powershell
$LASTEXITCODE
```

Stress, latency, dan checkout dengan injeksi error **sengaja dirancang melanggar SLO**. Mendapat `SLO FAIL` pada kasus itu merupakan hasil belajar yang diharapkan, bukan alasan mengganti threshold agar semua terlihat hijau.

## 5. Load — katalog pada jam sibuk

### 5.1 Pertanyaan dan kebutuhan kualitas

**Studi kasus:** toko online biasanya menerima 50 kunjungan katalog per detik pada jam sibuk. Kita ingin memeriksa apakah beban normal tersebut bisa dilayani secara stabil.

Quality attribute yang diuji adalah perilaku waktu dan kapasitas pada beban yang direncanakan. SLO latihan:

- Successful throughput **≥ 49 RPS**.
- p95 respons sukses **≤ 200 ms**.
- Error rate **< 1%**.

Toleransi throughput 2% memberi ruang untuk variasi pengukuran pada latihan singkat. Ini tidak berarti kita sengaja merencanakan kehilangan 2% request; jumlah sampel tetap diperiksa.

### 5.2 Rancangan eksperimen

1. Warm-up melakukan ramp dari 0 menuju 50 iterasi/detik selama **30 detik**.
2. Fase pengukuran mempertahankan 50 iterasi/detik selama **120 detik**.
3. Statistik warm-up tidak dimasukkan ke hasil utama.
4. Karena itu RPS dan persentil akhir menilai fase stabil, bukan mencampurnya dengan ramp.

Kode inti, yang sudah ada di runner:

```csharp
CreateScenario("load_catalog", "/api/catalog")
    .WithoutWarmUp()
    .WithWarmUpSimulations(
        Simulation.RampingInject(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)))
    .WithLoadSimulations(
        Simulation.Inject(50, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(120)))
```

`WithoutWarmUp()` membersihkan konfigurasi warm-up durasi bawaan helper, lalu `WithWarmUpSimulations` memasang pemanasan khusus. Warm-up tetap berjalan mengikuti simulasi khusus tersebut. Perilaku pemanasan tersedia di [dokumentasi Scenario](https://nbomber.com/docs/nbomber/scenario/).

### 5.3 Jalankan langkah demi langkah

1. Pastikan Terminal A masih menjalankan API.
2. Pindah ke Terminal B.
3. Jalankan:

```text
dotnet run --project PerformanceTests -c Release --no-build -- load
```

4. Tunggu sekitar 2 menit 30 detik, ditambah inisialisasi dan pembuatan report.
5. Cari baris `error_rate`, `p95`, `throughput`, dan `SLO PASS/FAIL`.
6. Buka report melalui path `Summary:` yang dicetak program; petunjuk lengkap ada di bagian 10.

### 5.4 Perhitungan dan hasil yang diharapkan

Jumlah request terjadwal pada fase ukur sekitar `50 × 120 = 6.000`. Sebagai ilustrasi, jika 6.000 semuanya sukses, successful throughput sekitar `6.000 / 120 = 50 RPS` dan error rate 0%.

**Contoh ilustratif, bukan hasil pengukuran mesin Anda:**

```text
Successful RPS=50,00; p95 OK=35 ms; error=0,00%
error_rate: PASS
p95: PASS
throughput: PASS
SLO PASS
```

Jika p95 280 ms, hasil harus FAIL walaupun seluruh request berstatus 200. Jika laju sukses hanya 35 RPS, selidiki server dan kemampuan generator; jangan menyimpulkan kapasitas memadai hanya karena error 0%.

### 5.5 Kesimpulan yang boleh dibuat

“Pada mesin, konfigurasi, dan durasi uji ini, endpoint katalog memenuhi/tidak memenuhi SLO pada 50 request/detik setelah warm-up.” Test ini belum membuktikan kapasitas maksimum, ketahanan 24 jam, maupun performa jaringan produksi.

## 6. Stress — stok saat flash sale

### 6.1 Pertanyaan dan kebutuhan kualitas

**Studi kasus:** flash sale meningkatkan permintaan cek stok jauh di atas beban normal. Kita ingin melihat kapan kualitas mulai memburuk, bagaimana server menolak kelebihan pekerjaan, dan apakah kondisinya membaik setelah lonjakan berakhir.

Target inventory hanya mempunyai **20 slot**. Setiap pekerjaan menahan satu slot sekitar **0,2 detik**. Perkiraan ideal kapasitas pelayanan adalah:

```text
20 slot / 0,2 detik = 100 request sukses/detik
```

Ini perhitungan model buatan, **bukan hasil benchmark**. Waktu antre, distribusi kedatangan, overhead HTTP, dan scheduler dapat mengubah titik masalah yang teramati.

### 6.2 Rancangan eksperimen

Sesudah warm-up 10 detik, satu run menjalankan fase-fase berikut **berurutan tanpa jeda yang sengaja ditambahkan**:

| Waktu fase ukur | Offered load | Tujuan |
| --- | --- | --- |
| 0–30 detik | 50 iterasi/detik | Baseline di bawah kapasitas perkiraan |
| 30–60 detik | 100 iterasi/detik | Mendekati batas model |
| 60–90 detik | 150 iterasi/detik | Melampaui batas |
| 90–120 detik | 250 iterasi/detik | Overload lebih berat |
| 120–150 detik | 50 iterasi/detik | Mengamati pemulihan |

SLO layanan latihan yang diperiksa otomatis adalah **error rate agregat < 1%**. Kita mengharapkan check ini **FAIL** karena offered load pada fase tinggi melampaui kapasitas. Uji demonstrasi berhasil jika pelanggaran itu terdeteksi dengan sampel valid dan kegagalannya berupa HTTP 503.

**Pemeriksaan titik jenuh dan pemulihan tetap dilakukan per fase di grafik.** Check agregat tidak membuktikan kapan masalah mulai muncul maupun apakah fase terakhir sudah pulih.

### 6.3 Jalankan

Di Terminal B:

```text
dotnet run --project PerformanceTests -c Release --no-build -- stress
```

Tunggu sekitar 2 menit 40 detik ditambah overhead. Expected result adalah `error_rate: FAIL`, `SLO FAIL`, exit `1`, dengan status kegagalan HTTP 503 dan `executionValid: true` pada summary.

### 6.4 Baca hasil per fase

1. Buka report HTML run stress.
2. Lihat grafik waktu, bukan hanya ringkasan seluruh run.
3. Cocokkan sumbu waktu dengan lima fase pada tabel. Bila grafik memakai waktu absolut, gunakan waktu mulai fase bombing/pengukuran di log sebagai acuan; warm-up 10 detik terpisah.
4. Cari fase pertama ketika failed RPS meningkat atau latensi bertambah nyata.
5. Periksa apakah successful RPS berhenti bertambah sebanding dengan offered load.
6. Pada fase terakhir, lihat apakah error rate turun dan latensi mendekati baseline. Beri perhatian khusus pada beberapa detik awal ketika antrean lama masih dikuras.

Contoh pola ilustratif:

| Offered RPS | Successful RPS | Gejala |
| --- | --- | --- |
| 50 | Mendekati 50 | Hampir semua sukses |
| 100 | Mendekati batas kemampuan aktual | Antrean/latensi mulai bertambah |
| 150 | Tidak mencapai 150 | Sebagian HTTP 503 |
| 250 | Cenderung mendatar | Penolakan lebih banyak |
| 50 kembali | Menuju baseline | Pemulihan setelah antrean selesai |

Jangan menghitung error rate fase 250 RPS memakai total request semua fase. Gunakan jumlah gagal dan total request **pada fase yang sama**. Jangan mengklaim batasnya tepat 100 RPS hanya karena rumus resource pool menghasilkan angka itu; untuk mempersempit batas, ulangi dengan tingkat beban lebih rapat dan durasi lebih panjang.

### 6.5 Interpretasi atribut kualitas

HTTP 503 terkontrol menunjukkan mekanisme penolakan bekerja pada model ini. Namun, keberhasilan menolak bukan keberhasilan melayani transaksi. Kebutuhan error rate < 1% tetap dilanggar.

Jika setelah kembali ke 50 RPS layanan tetap lambat, investigasi antrean yang tidak habis, resource yang tidak dilepas, atau bottleneck lain. API latihan memakai `finally` untuk mengembalikan slot; aplikasi nyata bisa memiliki perilaku yang lebih kompleks. Jangan menyatakan pemulihan berhasil hanya dari exit code stress.

## 7. Throughput — kapasitas layanan harga

### 7.1 Pertanyaan dan kebutuhan kualitas

**Studi kasus:** layanan harga harus melayani 300 permintaan per detik dari aplikasi toko. Kita ingin mengetahui apakah throughput sukses mengikuti laju masuk, dan apa yang berubah ketika laju dinaikkan.

Quality attribute yang diuji adalah kapasitas, dengan guardrail waktu respons dan kegagalan. Pada setiap tingkat beban, SLO latihan:

- Successful RPS **≥ 98% dari offered RPS**.
- p95 respons sukses **≤ 200 ms**.
- Error rate **< 1%**.

API memiliki 20 slot × waktu kerja 40 ms. Perkiraan idealnya `20 / 0,04 = 500 sukses/detik`. Angka ini memberi hipotesis bahwa 300 RPS dapat dilayani, sedangkan 600 RPS berpotensi melampaui kemampuan.

### 7.2 Jalankan tiga tingkat pada studi kasus yang sama

Masing-masing run mempunyai warm-up 10 detik dan pengukuran 60 detik. Jalankan satu baris, tunggu selesai, catat hasil, baru lanjut baris berikutnya:

```text
dotnet run --project PerformanceTests -c Release --no-build -- throughput 100
dotnet run --project PerformanceTests -c Release --no-build -- throughput 300
dotnet run --project PerformanceTests -c Release --no-build -- throughput 600
```

Angka terakhir adalah **offered RPS**, bukan jumlah user. Ini tetap satu studi kasus layanan harga dengan tiga tingkat eksperimen, bukan tiga cerita berbeda.

### 7.3 Hitung dan bandingkan

Untuk run 300, request terjadwal sekitar `300 × 60 = 18.000`. Jika ada 17.940 request sukses dan 60 gagal dalam 60 detik:

```text
Successful RPS = 17.940 / 60 = 299
Error rate = 60 / 18.000 × 100% = 0,333%
Batas throughput = 300 × 0,98 = 294 RPS
```

Throughput dan error rate tersebut memenuhi batas. Keputusan akhir masih memerlukan p95 ≤ 200 ms.

Isi tabel dengan hasil **aktual** dari masing-masing summary:

| Offered RPS | Successful RPS | p95 sukses (ms) | Error (%) | SLO PASS/FAIL |
| --- | --- | --- | --- | --- |
| 100 | … | … | … | … |
| 300 | … | … | … | … |
| 600 | … | … | … | … |

**Pola yang diharapkan:** 100 dan 300 memenuhi SLO pada mesin yang memadai; 600 berpotensi gagal pada throughput, p95, dan/atau error rate. Jangan memaksa hasil sesuai dugaan. Catat tingkat tertinggi **yang benar-benar diuji** dan masih memenuhi seluruh SLO sebagai batas bawah kapasitas yang terbukti, bukan kapasitas maksimum yang pasti.

### 7.4 Kesalahan yang harus dihindari

Jika run 600 mencatat 600 completed RPS tetapi 150 di antaranya adalah kegagalan, successful throughput hanya 450 RPS. Satuan RPS belum cukup: selalu jelaskan apakah angka tersebut adalah request masuk, selesai, atau sukses.

Dalam tutorial ini throughput berbentuk request/detik. Pada sistem lain, satuan yang relevan dapat berupa pesan/detik, transaksi/detik, atau byte/detik; jelaskan unit sebelum membandingkan angka.

## 8. Latency percentiles — pencarian produk

### 8.1 Pertanyaan dan kebutuhan kualitas

**Studi kasus:** pencarian produk terasa cepat untuk sebagian besar pengunjung, tetapi beberapa orang mengeluh sangat lambat. Rata-rata waktu respons saja mungkin menyembunyikan pengalaman buruk tersebut.

Quality attribute yang diuji adalah perilaku waktu, terutama **tail latency** atau ekor distribusi. SLO latihan pada 50 request/detik:

- p95 respons sukses **≤ 300 ms**.
- p99 respons sukses **≤ 800 ms**.
- Error rate **< 1%**.

API sengaja membuat 90% request menunggu 40 ms, 8% 250 ms, dan 2% 1.200 ms. Semua tetap bisa HTTP 200. Latensi aktual akan sedikit lebih besar daripada delay karena overhead.

### 8.2 Makna persentil

| Metrik | Cara membacanya |
| --- | --- |
| p50 = 45 ms | Sekitar 50% sampel selesai dalam 45 ms atau lebih cepat |
| p95 = 260 ms | Sekitar 95% sampel selesai dalam 260 ms atau lebih cepat |
| p99 = 1.210 ms | Sekitar 99% sampel selesai dalam 1.210 ms atau lebih cepat |

p99 **bukan** rata-rata dari 1% request terlambat dan bukan request paling lambat. Ia adalah batas persentil ke-99. Report NBomber menjelaskan persentil serta kelompok statistik sukses/gagal di [dokumentasi Reports](https://nbomber.com/docs/reporting/reports/).

### 8.3 Mengapa rata-rata dapat menipu?

Mengabaikan overhead, rata-rata model API ini sekitar:

```text
(0,90 × 40) + (0,08 × 250) + (0,02 × 1.200)
= 36 + 20 + 24
= 80 ms
```

Angka 80 ms terdengar baik, tetapi sekitar 2% request menunggu lebih dari satu detik. Dengan distribusi itu, p95 cenderung berada dekat kelompok 250 ms dan p99 dekat kelompok 1.200 ms. Karena kelompok lambatnya 2%, p99 dapat menangkapnya.

### 8.4 Jalankan dan putuskan

```text
dotnet run --project PerformanceTests -c Release --no-build -- latency
```

Tunggu warm-up 10 detik dan pengukuran 60 detik. Perkiraan sampel utama adalah `50 × 60 = 3.000` request. Karena pemilihan delay acak, proporsi aktual tidak harus tepat 90/8/2 persen.

Hasil yang diharapkan:

1. Hampir seluruh request sukses.
2. Check `error_rate` lulus.
3. p95 cenderung memenuhi 300 ms pada mesin yang memadai.
4. p99 melampaui 800 ms.
5. Program menghasilkan `SLO FAIL`, exit `1`.

Kegagalan ini menunjukkan pelanggaran kualitas waktu, walaupun fungsi pencariannya benar dan error rate 0%. Jangan menaikkan batas p99 hanya agar hasil lulus; cari operasi yang jarang tetapi lambat melalui tracing/profiling pada aplikasi nyata.

### 8.5 Batas interpretasi

Kode menggunakan `stats.Ok.Latency`, jadi persentil tersebut hanya untuk **request sukses**. Jika request lambat berubah menjadi timeout, request itu masuk kelompok gagal dan p99 sukses mungkin tampak membaik. Karena itu persentil harus dibaca bersama jumlah gagal dan statistik latensi kegagalan.

Jangan merata-ratakan p99 dari beberapa mesin atau beberapa run untuk mendapatkan p99 gabungan. Persentil gabungan memerlukan distribusi/histogram atau sampel yang dapat digabungkan. Untuk p99 yang stabil, ulangi dengan sampel lebih banyak; run 60 detik adalah latihan awal, bukan bukti final pengalaman semua pengguna.

## 9. Error rate — checkout ketika payment bermasalah

### 9.1 Pertanyaan dan kebutuhan kualitas

**Studi kasus:** checkout memanggil payment provider yang kadang tidak tersedia. Kita ingin memastikan kegagalannya terlihat dalam metrik dan SLO menolak kondisi buruk tersebut.

Quality attribute yang diuji adalah keandalan pada kondisi eksperimen. SLO latihan:

- Error rate **< 1%**.
- p95 respons sukses **≤ 200 ms**.

Dalam contoh ini seluruh HTTP non-2xx dianggap kegagalan. Aplikasi nyata mungkin mempunyai 4xx yang memang diharapkan; definisi keberhasilan harus ditentukan berdasarkan kontrak bisnis sebelum test, bukan diubah setelah melihat hasil.

### 9.2 Jalankan kontrol sehat terlebih dahulu

Di Terminal B:

```text
dotnet run --project PerformanceTests -c Release --no-build -- error-rate 0
```

Angka `0` berarti API tidak menginjeksi kegagalan payment. Setelah sekitar 70 detik, hasil yang diharapkan adalah `SLO PASS`, exit `0`. Kontrol ini membantu memastikan alat, endpoint, dan lingkungan dasar bekerja.

### 9.3 Jalankan kondisi payment bermasalah

Setelah kontrol sehat selesai:

```text
dotnet run --project PerformanceTests -c Release --no-build -- error-rate 10
```

Angka `10` berarti **probabilitas** 10% setiap request menghasilkan HTTP 503. Ini bukan jaminan tepat satu dari sepuluh request dan bukan 10 request gagal per detik. Beban masuk tetap 50 request/detik.

Hasil yang diharapkan adalah error rate mendekati 10%, check `error_rate: FAIL`, `SLO FAIL`, exit `1`. Dalam 3.000 request, perkiraan jumlah gagal sekitar 300, tetapi hasil aktual bisa berbeda karena acak.

### 9.4 Hitung sendiri dari summary

Misalnya summary menunjukkan:

```text
okCount = 2.706
failCount = 294
total = 3.000
```

Maka:

```text
Error rate = 294 / 3.000 × 100% = 9,8%
Success rate = 2.706 / 3.000 × 100% = 90,2%
Successful throughput ≈ 2.706 / 60 = 45,1 RPS
```

Pembagi error rate adalah **seluruh request**, bukan jumlah sukses. Jangan memakai `294 / 2.706`.

Jika target < 1% dan sampel tepat 3.000, jumlah gagal maksimal yang masih memenuhi batas adalah **29**. Sebanyak 30 gagal berarti tepat 1%, sehingga tidak memenuhi operator `< 1`.

### 9.5 Mengapa retry tidak ditambahkan?

Retry dapat membuat operasi bisnis akhirnya sukses, tetapi menambah request ke payment serta waktu tunggu. Jika ditambahkan tanpa memisahkan metrik, error rate permintaan pertama bisa tersembunyi. Latihan ini sengaja tanpa retry agar satu request gagal tetap terlihat jelas.

Pada aplikasi nyata, pisahkan keberhasilan upaya pertama, jumlah percobaan ulang, keberhasilan transaksi akhir, dan latensi transaksi akhir. Checkout 100% sukses sesudah retry belum tentu memenuhi target waktu pengguna.

## 10. Membaca report dan menulis kesimpulan

### 10.1 Cari output setiap run

Program membuat folder unik:

```text
reports/
└── nama-kasus-waktu-id/
    ├── summary.json
    └── ... report NBomber HTML, CSV, dan Markdown ...
```

NBomber dapat membuat subfolder sesi di bawah folder tersebut. Gunakan pencarian file jika report `.html` tidak langsung terlihat. `Summary:` pada akhir output menunjukkan lokasi absolut `summary.json`.

1. Buka folder repository di File Explorer/Finder/file manager.
2. Masuk ke `reports`.
3. Pilih folder sesuai kasus dan waktu run terakhir.
4. Buka `summary.json` memakai editor teks.
5. Temukan file `.html` di folder yang sama atau subfoldernya, lalu klik dua kali untuk membuka di browser.

Tidak perlu server web untuk melihat HTML report lokal. Anda juga tidak perlu spreadsheet untuk membaca summary JSON.

### 10.2 Pahami isi summary

| Field | Makna |
| --- | --- |
| `selected` | Kasus yang dijalankan |
| `rate` | Nilai konfigurasi RPS untuk kasus throughput; nilai bawaan pada kasus lain tidak menyatakan bebannya |
| `failurePercent` | Konfigurasi injeksi error checkout; tidak dipakai endpoint lain |
| `plannedRequests` | Perkiraan jumlah request terjadwal pada fase ukur |
| `executionValid` | Ada sukses, sampel ≥ 95% rencana, dan tidak ada kode gagal di luar HTTP 503 |
| `sloPassed` | Run valid dan semua check yang dipasang terpenuhi |
| `okCount`, `failCount`, `total` | Jumlah sukses, gagal, dan total |
| `errorRate` | Persentase gagal pada seluruh request |
| `successfulRps` | RPS kelompok sukses dari statistik NBomber |
| `p95`, `p99` | Persentil latensi kelompok sukses, satuan milidetik |
| `failureCodes` | Jenis kode kegagalan yang teramati |
| `checks` | Nama pemeriksaan beserta nilai true/false |

Guard sampel 95% adalah pemeriksaan kasar agar run yang berhenti di tengah tidak diterima sebagai demonstrasi berhasil. Guard ini bukan bukti lengkap bahwa distribusi kedatangan tepat; validasi generator dan grafik tetap diperlukan.

### 10.3 Bedakan threshold runtime dan keputusan akhir

Runner menambahkan threshold error rate < 1% untuk ditampilkan pada report NBomber. Threshold runtime mengecek statistik berkala dan tidak disetel untuk menghentikan run ini lebih awal. Keputusan akhir program dihitung eksplisit sesudah `.Run()` agar exit code dapat diperiksa CI. Mekanisme statistik dan threshold dijelaskan pada [Asserts and Thresholds](https://nbomber.com/docs/nbomber/asserts_and_thresholds/).

Sebuah threshold runtime bisa sempat gagal pada satu interval meski agregat akhir memenuhi batas. Karena itu `sloPassed` di summary berarti **check agregat akhir tutorial**, bukan janji bahwa semua interval selalu sehat. Pada stress, gunakan grafik per fase untuk pertanyaan waktu dan pemulihan.

### 10.4 Template catatan hasil

Salin dan isi template ini untuk setiap kasus:

```text
Kasus:
Tanggal / commit kode:
OS / CPU / RAM / versi .NET:
Endpoint:
Pola beban dan durasi:
Definisi sukses dan gagal:
SLO:
Jumlah sukses / gagal / total:
Successful RPS:
p95 / p99 sukses:
Error rate dan jenis kegagalan:
Hasil check:
Lokasi report:
Kesimpulan yang didukung data:
Keterbatasan / eksperimen berikutnya:
```

Pisahkan **hasil yang diukur** dari **dugaan penyebabnya**. “p99 1.220 ms” adalah observasi. “Database lambat” masih hipotesis sampai ada telemetry server yang mendukung.

## 11. GitHub Actions: membuktikan PASS dan FAIL di tiga OS

### 11.1 Apa yang dilakukan workflow?

File `.github/workflows/nbomber.yml` berjalan pada push, pull request, atau ketika dipicu manual. Matriksnya menjalankan enam eksperimen pada **Ubuntu, macOS, dan Windows**, sehingga total **18 job**:

| Nama eksperimen CI | Perintah kasus | Hasil SLO yang diharapkan | Check gagal yang diterima |
| --- | --- | --- | --- |
| `load-pass` | `load` | PASS | Tidak ada |
| `throughput-pass` | `throughput 300` | PASS | Tidak ada |
| `checkout-pass` | `error-rate 0` | PASS | Tidak ada |
| `stress-detect-overload` | `stress` | FAIL | Hanya `error_rate` |
| `latency-detect-tail` | `latency` | FAIL | Hanya `p99` |
| `checkout-detect-errors` | `error-rate 10` | FAIL | Hanya `error_rate` |

Mengapa expected FAIL bisa menghasilkan **job hijau**? Karena tujuan tiga job terakhir adalah membuktikan bahwa alat **mendeteksi pelanggaran yang disengaja**. Job hijau berarti hasil eksperimen sesuai rancangan; bukan berarti endpoint yang melanggar SLO dianggap sehat.

Jika latency tidak lagi melanggar p99, demonstrasi negative control gagal. Jika p95 juga melanggar padahal hanya p99 yang diharapkan gagal, CI juga menolak hasil tersebut. Ini lebih ketat daripada mengabaikan semua exit nonzero.

### 11.2 Alur setiap job

1. Checkout repository.
2. Pasang .NET SDK 10 dan Python 3.13.
3. Periksa kode lengkap di README sama dengan file sumbernya.
4. Build API dan runner dalam Release.
5. Nyalakan API lokal secara otomatis.
6. Tunggu `/health` siap; jangan hanya tidur beberapa detik lalu berasumsi API hidup.
7. Jalankan satu kasus NBomber.
8. Periksa summary baru, sampel valid, check yang gagal, status SLO, serta exit code.
9. Untuk checkout gagal, pastikan error rate juga berada pada rentang demonstrasi 5–15%, bukan seluruh operasi gagal.
10. Hentikan proses API, termasuk ketika test gagal.
11. Unggah report dan log sebagai artifact, termasuk ketika job gagal.

Pengelolaan proses memakai Python standard library agar tidak bergantung pada `&`, `kill`, `curl`, atau Bash. Kode eksperimen performanya tetap C# dan NBomber. Workflow memakai action resmi [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), [setup-python](https://github.com/actions/setup-python), dan [upload-artifact](https://github.com/actions/upload-artifact).

### 11.3 Menjalankan workflow lewat antarmuka GitHub

Setelah file repository ini tersedia di branch GitHub Anda:

1. Buka repository pada GitHub.
2. Klik tab **Actions**.
3. Jika diminta mengaktifkan Actions pada fork, aktifkan terlebih dahulu.
4. Pilih workflow **NBomber quality attributes** di sebelah kiri.
5. Klik **Run workflow**, pilih branch, lalu klik tombol konfirmasi **Run workflow**. Pemicu manual biasanya baru muncul ketika file workflow sudah ada di default branch.
6. Buka run yang baru muncul.
7. Klik job yang ingin dilihat, misalnya `windows-latest / latency-detect-tail`.
8. Buka langkah **Run experiment and check expected result**.
9. Perhatikan perbedaan `SLO FAIL` dari program C# dan `CI PASS` dari pembungkus demonstrasi.

Workflow tidak memerlukan API key, database, maupun secret eksternal. Tutorial ini untuk penggunaan edukasi/personal; jika mengadaptasinya untuk organisasi, tinjau [ketentuan lisensi NBomber](https://nbomber.com/docs/getting-started/license/).

### 11.4 Mengunduh hasil dari GitHub

1. Kembali ke halaman utama run workflow.
2. Cari bagian **Artifacts**.
3. Unduh artifact dengan nama OS dan eksperimen yang ingin dipelajari.
4. Ekstrak ZIP.
5. Buka `summary.json` dan report `.html` di folder hasil ekstrak.
6. Jika gagal sebelum menghasilkan report, baca `logs/api.log` dan `logs/nbomber.log`, lalu log build di halaman job.

Artifact disimpan selama **14 hari** oleh konfigurasi workflow. Ringkasan angka juga ditulis ke halaman summary job.

### 11.5 Meniru pembungkus CI secara lokal (opsional)

Bagian ini hanya perlu Python jika Anda ingin API dinyalakan/dimatikan otomatis seperti CI. **Hentikan API Terminal A dengan Ctrl+C terlebih dahulu**, karena pembungkus menolak port 5080 yang sudah dipakai.

Setelah kedua proyek di-build, pada Windows gunakan:

```powershell
python scripts/ci_test.py load
python scripts/ci_test.py latency --expect-fail p99
python scripts/ci_test.py error-rate --value 10 --expect-fail error_rate
```

Pada Linux/macOS gunakan:

```bash
python3 scripts/ci_test.py load
python3 scripts/ci_test.py latency --expect-fail p99
python3 scripts/ci_test.py error-rate --value 10 --expect-fail error_rate
```

Jalankan satu per satu. API otomatis dihentikan setelah tiap eksperimen. Tidak perlu menjalankan dua terminal untuk mode pembungkus ini.

## 12. Validitas eksperimen dan batas kesimpulan

1. **Satu mesin adalah lingkungan belajar.** API dan generator berbagi CPU/RAM. Untuk capacity planning produksi, pisahkan host generator dan server.
2. **Model API sengaja sederhana.** `Task.Delay` meniru tunggu I/O. Hasilnya tidak memprediksi performa database, disk, atau CPU aplikasi nyata.
3. **Gunakan Release dan catat lingkungan.** Bandingkan run dengan versi kode, resource, protokol, dan durasi yang setara.
4. **Warm-up bukan beban utama.** Program memakai 10 detik pemanasan pada kebanyakan kasus. Load memakai ramp warm-up khusus 30 detik. Warm-up dapat memengaruhi state server meski sampelnya dikeluarkan.
5. **Jangan hanya melihat rata-rata agregat.** Stress membutuhkan grafik per fase; angka gabungan dapat menyembunyikan overload singkat dan kegagalan pemulihan.
6. **Jangan mengabaikan kegagalan saat menilai persentil.** p99 sukses bisa membaik ketika request lambat malah timeout.
7. **Ulangi eksperimen.** Hasil yang berubah-ubah membutuhkan investigasi dan run lebih panjang. Jangan memilih satu run terbaik sebagai bukti kapasitas.
8. **Pantau server dan generator.** CPU penuh pada generator bisa menyebabkan load tidak tercapai. CPU/RAM/koneksi server, antrean, tracing, dan log dibutuhkan untuk menemukan akar masalah.
9. **Runner GitHub berbagi infrastruktur.** Job lintas OS memeriksa bahwa materi dapat dijalankan dan hasil kontrolnya masuk akal; angkanya bukan benchmark perbandingan performa OS.
10. **Definisi valid tutorial dibatasi.** `executionValid` adalah guard otomatis dasar, bukan sertifikasi mutu statistik. Jika ada jenis error selain 503, investigasi meski error rate numeriknya rendah.

## 13. Troubleshooting


| Gejala | Kemungkinan penyebab | Langkah perbaikan |
| --- | --- | --- |
| `dotnet` tidak dikenali | SDK belum terpasang/PATH belum diperbarui | Pasang SDK 10, buka terminal baru, cek `dotnet --version` |
| Tidak ada `Microsoft.AspNetCore.App` | Runtime web belum terpasang | Pasang ASP.NET Core runtime 10 untuk OS/arsitektur Anda |
| `NETSDK1045` | SDK aktif tidak mendukung .NET 10 | Periksa `dotnet --list-sdks`; gunakan SDK 10 atau lebih baru yang mendukung target |
| `NETSDK1226` pada distro .NET khusus | Data SDK/framework pruning tidak lengkap | Gunakan distribusi SDK resmi; untuk diagnosis lokal build dengan `-p:AllowMissingPrunePackageData=true`, lalu pastikan runtime web tersedia |
| `Project file does not exist` | Terminal berada di folder salah | Masuk ke folder yang berisi README, DemoApi, PerformanceTests |
| Restore `NU1301`/gagal NuGet | Koneksi, proxy, atau sertifikat | Pastikan akses ke NuGet; perbaiki koneksi/proxy lalu ulangi restore |
| `Address already in use` / port 5080 dipakai | API lain atau run lama masih hidup | Kembali ke terminal API lama lalu Ctrl+C; jangan menyalakan dua API pada port yang sama |
| `API belum siap` / connection refused | API belum hidup atau port salah | Buka `/health` di browser dan baca Terminal A |
| `--no-build` tidak menemukan executable | Proyek belum di-build atau output telah dihapus | Ulangi kedua perintah build Release |
| Report tidak ditemukan | Membuka run/folder yang salah | Ikuti path `Summary:` dan cari file `.html` secara rekursif di folder run tersebut |
| `INVALID` / sampel kurang | Run terputus, generator tidak sanggup, error tak sesuai latihan | Baca log, jumlah sampel, `failureCodes`; jangan menganggapnya expected FAIL |
| Latency/error-rate menghasilkan exit 1 | Pelanggaran memang disengaja | Cocokkan check yang gagal dengan tabel hasil yang diharapkan |
| Baseline CI merah | Mesin sibuk atau regresi nyata | Baca summary dan log; ulangi secara terkontrol, jangan langsung melonggarkan SLO |
| Workflow manual tidak muncul | Workflow belum ada di default branch/Actions belum aktif | Pastikan branch dan pengaturan Actions benar |
| Python tidak ditemukan | Hanya diperlukan untuk pembungkus CI | Jalankan langkah manual `dotnet`, atau pasang Python dan gunakan `python3` pada Linux/macOS |

Jika mengubah file C#, lakukan build lagi sebelum menjalankan perintah dengan `--no-build`. Setelah selesai latihan, hentikan Terminal A menggunakan **Ctrl+C**.

## 14. Penjelasan kode C# dan kode lengkap

### 14.1 Alur program penguji

1. Baca nama kasus dan argumen angka jika ada.
2. Buat folder report unik agar hasil sebelumnya tetap tersimpan.
3. Buat satu `HttpClient` yang dipakai ulang selama run.
4. Cek `/health` sebelum memulai benchmark.
5. Buat scenario dan pilih profil beban sesuai kasus.
6. Jalankan NBomber.
7. Ambil statistik sukses/gagal dari hasil run.
8. Validasi sampel dan jenis error.
9. Evaluasi SLO dengan operator yang eksplisit.
10. Tulis `summary.json`, cetak hasil, dan kembalikan exit code.

### 14.2 Mengapa detail implementasi ini penting?

- **`HttpClient` dipakai ulang:** tidak menambahkan biaya pembuatan client/koneksi baru setiap iterasi.
- **`using` pada request/response/JSON:** melepaskan resource sesudah request selesai.
- **Timeout 3 detik:** request tidak menunggu tanpa batas; timeout menjadi kegagalan.
- **`IsSuccessStatusCode`:** HTTP 4xx/5xx dipetakan ke `Response.Fail`. Menggunakan `HttpClient` langsung berarti kita harus menetapkan klasifikasi ini sendiri.
- **Validasi `ok: true`:** HTTP 200 dengan body yang melanggar kontrak tetap dianggap gagal. Ini validasi minimal, bukan pemeriksaan seluruh aturan bisnis.
- **`Response.Ok`:** menandai operasi berhasil bagi NBomber.
- **`sizeBytes`:** ukuran body sukses saja, bukan seluruh byte jaringan termasuk header/TLS.
- **`Response.Fail`:** merekam status HTTP atau kode seperti `timeout`, `network_error`, dan `invalid_json`.
- **`SemaphoreSlim`:** meniru keterbatasan resource. `finally` memastikan slot dikembalikan walaupun pekerjaan dibatalkan.
- **`Random.Shared`:** aman dipakai paralel untuk memilih kelompok delay/kegagalan, sehingga jumlah aktual bervariasi antarrun.
- **Tanpa retry:** jumlah request dan error tidak tersamarkan.
- **Server GC pada runner:** konfigurasi runtime generator ditetapkan eksplisit agar lebih sesuai untuk beban paralel.
- **Path report absolut:** hasil disimpan di bawah direktori repository tempat perintah dijalankan, bukan bergantung pada lokasi executable `dotnet`.

### 14.3 Jika ingin membuat ulang proyek secara manual

Bagian ini **opsional**; lewati jika memakai repository yang sudah disediakan.

1. Buat folder kosong, lalu buka terminal di folder itu.
2. Jalankan:

```text
dotnet new web -n DemoApi --framework net10.0 --no-restore
dotnet new console -n PerformanceTests --framework net10.0 --no-restore
```

3. Ganti **seluruh isi** kedua `.csproj` dan kedua `Program.cs` dengan kode lengkap di bawah. Jangan menempel kode baru di bawah kode bawaan template.
4. Jalankan restore/build pada bagian 3.
5. Jalankan API dan test pada bagian 4–9.

Kode berikut sama dengan file yang dipakai CI. Check otomatis akan gagal jika salinan README dan file sumber berbeda.

### `DemoApi/DemoApi.csproj`

<!-- source:DemoApi/DemoApi.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```
<!-- /source:DemoApi/DemoApi.csproj -->

### `DemoApi/Program.cs`

<!-- source:DemoApi/Program.cs -->
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();

// Meniru pool resource terbatas, bukan CPU atau database sungguhan.
using var inventorySlots = new SemaphoreSlim(20, 20);
using var priceSlots = new SemaphoreSlim(20, 20);

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/catalog", async (HttpContext context) =>
{
    await Task.Delay(30, context.RequestAborted);
    return Results.Ok(new { ok = true, productId = 1, name = "Kopi" });
});

app.MapGet("/api/inventory", async (HttpContext context) =>
{
    if (!await inventorySlots.WaitAsync(500, context.RequestAborted))
        return Results.Json(new { ok = false, error = "inventory_busy" }, statusCode: 503);
    try
    {
        await Task.Delay(200, context.RequestAborted);
        return Results.Ok(new { ok = true, productId = 1, stock = 25 });
    }
    finally { inventorySlots.Release(); }
});

app.MapGet("/api/prices", async (HttpContext context) =>
{
    if (!await priceSlots.WaitAsync(500, context.RequestAborted))
        return Results.Json(new { ok = false, error = "price_busy" }, statusCode: 503);
    try
    {
        await Task.Delay(40, context.RequestAborted);
        return Results.Ok(new { ok = true, productId = 1, price = 25000 });
    }
    finally { priceSlots.Release(); }
});

app.MapGet("/api/search", async (HttpContext context) =>
{
    int bucket = Random.Shared.Next(100);
    int delayMs = bucket < 90 ? 40 : bucket < 98 ? 250 : 1200;
    await Task.Delay(delayMs, context.RequestAborted);
    return Results.Ok(new { ok = true, query = "kopi", count = 5 });
});

app.MapPost("/api/checkout", async (HttpContext context) =>
{
    string raw = context.Request.Query["failurePercent"].ToString();
    if (!int.TryParse(raw, out int percent) || percent < 0 || percent > 100)
        return Results.BadRequest(new { ok = false, error = "invalid_percentage" });
    await Task.Delay(80, context.RequestAborted);
    if (Random.Shared.Next(100) < percent)
        return Results.Json(new { ok = false, error = "payment_unavailable" }, statusCode: 503);
    return Results.Ok(new { ok = true, orderId = Guid.NewGuid() });
});

app.Run();
```
<!-- /source:DemoApi/Program.cs -->

### `PerformanceTests/PerformanceTests.csproj`

<!-- source:PerformanceTests/PerformanceTests.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ServerGarbageCollection>true</ServerGarbageCollection>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NBomber" Version="6.7.1" />
  </ItemGroup>
</Project>
```
<!-- /source:PerformanceTests/PerformanceTests.csproj -->

### `PerformanceTests/Program.cs`

<!-- source:PerformanceTests/Program.cs -->
```csharp
using System.Text.Json;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// Argumen: kasus [angka]. Angka = RPS throughput atau persen gagal checkout.
string selected = args.FirstOrDefault()?.ToLowerInvariant() ?? "load";
string[] validCases = ["load", "stress", "throughput", "latency", "error-rate"];
if (!validCases.Contains(selected) || args.Length > 2)
{
    Console.Error.WriteLine("Gunakan: load | stress | throughput [RPS] | latency | error-rate [persen]");
    return 2;
}
int rate = 300;
int failurePercent = 10;
if (args.Length == 2)
{
    if (!int.TryParse(args[1], out int value)
        || (selected != "throughput" && selected != "error-rate")
        || (selected == "throughput" && (value < 1 || value > 2000))
        || (selected == "error-rate" && (value < 0 || value > 100)))
    {
        Console.Error.WriteLine("Throughput: 1..2000 RPS; error-rate: 0..100 persen.");
        return 2;
    }
    if (selected == "throughput") rate = value;
    else failurePercent = value;
}

string runId = $"{selected}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
string reportFolder = Path.GetFullPath(Path.Combine("reports", runId));
Directory.CreateDirectory(reportFolder);
string summaryPath = Path.Combine(reportFolder, "summary.json");

using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 1000 })
{
    BaseAddress = new Uri("http://127.0.0.1:5080"),
    Timeout = TimeSpan.FromSeconds(3)
};

try
{
    using var health = await http.GetAsync("/health");
    health.EnsureSuccessStatusCode();
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"API belum siap: {ex.Message}");
    return 2;
}

ScenarioProps CreateScenario(string name, string path, bool post = false)
{
    return Scenario.Create(name, async _ =>
    {
        try
        {
            using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, path);
            using var response = await http.SendAsync(request);
            byte[] body = await response.Content.ReadAsByteArrayAsync();
            string status = ((int)response.StatusCode).ToString();
            if (!response.IsSuccessStatusCode)
                return Response.Fail(statusCode: status, message: "HTTP non-2xx");
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("ok", out var ok)
                || ok.ValueKind != JsonValueKind.True)
                return Response.Fail(statusCode: "invalid_body", message: "ok harus true");
            return Response.Ok(statusCode: status, sizeBytes: body.Length);
        }
        catch (TaskCanceledException)
        {
            return Response.Fail(statusCode: "timeout", message: "Timeout client 3 detik");
        }
        catch (HttpRequestException)
        {
            return Response.Fail(statusCode: "network_error", message: "Koneksi gagal");
        }
        catch (JsonException)
        {
            return Response.Fail(statusCode: "invalid_json", message: "JSON tidak valid");
        }
    }).WithWarmUpDuration(TimeSpan.FromSeconds(10)).WithMaxFailCount(100_000);
}

TimeSpan Seconds(int value) => TimeSpan.FromSeconds(value);
var interval = Seconds(1);
var scenario = selected switch
{
    // Ramp berlangsung pada warm-up agar SLO hanya menilai fase stabil.
    "load" => CreateScenario("load_catalog", "/api/catalog")
        .WithoutWarmUp()
        .WithWarmUpSimulations(Simulation.RampingInject(50, interval, Seconds(30)))
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(120))),
    "stress" => CreateScenario("stress_inventory", "/api/inventory")
        .WithLoadSimulations(
            Simulation.Inject(50, interval, Seconds(30)),
            Simulation.Inject(100, interval, Seconds(30)),
            Simulation.Inject(150, interval, Seconds(30)),
            Simulation.Inject(250, interval, Seconds(30)),
            Simulation.Inject(50, interval, Seconds(30))),
    "throughput" => CreateScenario("throughput_prices", "/api/prices")
        .WithLoadSimulations(Simulation.Inject(rate, interval, Seconds(60))),
    "latency" => CreateScenario("latency_search", "/api/search")
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(60))),
    "error-rate" => CreateScenario("error_rate_checkout",
            $"/api/checkout?failurePercent={failurePercent}", post: true)
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(60))),
    _ => throw new InvalidOperationException()
};

// Runtime threshold ditampilkan di report; keputusan exit code tetap eksplisit di bawah.
scenario = scenario.WithThresholds(Threshold.Create(s => s.Fail.Request.Percent < 1));
var result = NBomberRunner.RegisterScenarios(scenario)
    .WithTestSuite("quality_attribute_tutorial").WithTestName(runId)
    .WithReportFolder(reportFolder)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Md).Run();
var stats = result.ScenarioStats.Single();
long okCount = stats.Ok.Request.Count;
long failCount = stats.Fail.Request.Count;
long total = okCount + failCount;
double errorRate = total > 0 ? 100.0 * failCount / total : 100;
double successfulRps = stats.Ok.Request.RPS;
double p95 = stats.Ok.Latency.Percent95;
double p99 = stats.Ok.Latency.Percent99;

// Guard: sebuah run yang terputus/kurang sampel tidak boleh dianggap expected failure.
int plannedRequests = selected switch
{
    "load" => 50 * 120,
    "stress" => (50 + 100 + 150 + 250 + 50) * 30,
    "throughput" => rate * 60,
    _ => 50 * 60
};
bool executionValid = okCount > 0 && total >= plannedRequests * 0.95;
// HTTP 503 merupakan kegagalan yang dirancang pada target latihan.
// Timeout/koneksi/body rusak tidak boleh menyamar sebagai expected failure CI.
var failureCodes = stats.Fail.StatusCodes.Select(x => x.StatusCode).ToArray();
executionValid &= failureCodes.All(code => code == "503");
var checks = new Dictionary<string, bool> { ["error_rate"] = errorRate < 1 };
if (selected != "stress") checks["p95"] = p95 <= (selected == "latency" ? 300 : 200);
if (selected == "load") checks["throughput"] = successfulRps >= 49;
if (selected == "throughput") checks["throughput"] = successfulRps >= rate * 0.98;
if (selected == "latency") checks["p99"] = p99 <= 800;

bool sloPassed = executionValid && checks.Values.All(x => x);
var summary = new
{
    selected, rate, failurePercent, plannedRequests, executionValid, sloPassed,
    okCount, failCount, total, errorRate, successfulRps, p95, p99, failureCodes, checks
};
File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nall={total}, ok={okCount}, fail={failCount}, error={errorRate:F2}%");
Console.WriteLine($"Successful RPS={successfulRps:F2}, p95 OK={p95:F2} ms, p99 OK={p99:F2} ms");
foreach (var check in checks) Console.WriteLine($"{check.Key}: {(check.Value ? "PASS" : "FAIL")}");
Console.WriteLine($"Summary: {summaryPath}");
Console.WriteLine(!executionValid ? "INVALID: sampel tidak cukup." : sloPassed ? "SLO PASS" : "SLO FAIL");
return !executionValid ? 2 : sloPassed ? 0 : 1;
```
<!-- /source:PerformanceTests/Program.cs -->

## 15. Kode lengkap workflow dan pengujinya

Kode ini sudah disediakan di repository. Anda cukup membaca alurnya; tidak perlu mengetik ulang untuk menjalankan tutorial. Pembungkus Python hanya memakai standard library dan tidak memerlukan `pip install`.

`ci_test.py` mempunyai dua tugas terpisah: mengelola masa hidup proses API dan memeriksa bahwa hasil eksperimen sesuai harapan. Ia tidak mengubah threshold C# agar negative control menjadi lulus. `check_readme.py` memeriksa salinan blok kode README saat workflow berjalan.

### `.github/workflows/nbomber.yml`

<!-- source:.github/workflows/nbomber.yml -->
```yaml
name: NBomber quality attributes

on:
  push:
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  tutorial:
    name: ${{ matrix.os }} / ${{ matrix.example.name }}
    runs-on: ${{ matrix.os }}
    timeout-minutes: 12
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]
        example:
          - name: load-pass
            args: load
          - name: throughput-pass
            args: throughput --value 300
          - name: checkout-pass
            args: error-rate --value 0
          - name: stress-detect-overload
            args: stress --expect-fail error_rate
          - name: latency-detect-tail
            args: latency --expect-fail p99
          - name: checkout-detect-errors
            args: error-rate --value 10 --expect-fail error_rate
    env:
      DOTNET_NOLOGO: 'true'
      DOTNET_CLI_TELEMETRY_OPTOUT: 'true'
      PYTHONUTF8: '1'
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - uses: actions/setup-python@v7
        with:
          python-version: '3.13'
      - name: Check README code matches files
        run: python scripts/check_readme.py
      - name: Test PASS and FAIL verification
        run: python -m unittest discover -s scripts -p "test_*.py"
      - name: Build API
        run: dotnet build DemoApi -c Release
      - name: Build NBomber runner
        run: dotnet build PerformanceTests -c Release
      - name: Run experiment and check expected result
        run: python scripts/ci_test.py ${{ matrix.example.args }}
      - name: Upload reports and logs even when a check fails
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: nbomber-${{ matrix.os }}-${{ matrix.example.name }}
          path: |
            reports/
            logs/
          if-no-files-found: warn
          retention-days: 14
```
<!-- /source:.github/workflows/nbomber.yml -->

### `scripts/ci_test.py`

<!-- source:scripts/ci_test.py -->
```python
"""Menjalankan satu eksperimen beserta API lokal di Linux/macOS/Windows."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request


def verify(summary, actual_exit, expected_failures):
    if not summary["executionValid"]:
        raise RuntimeError("Run invalid: sampel kurang atau jenis error tidak sesuai latihan")
    failed = sorted(key for key, passed in summary["checks"].items() if not passed)
    if failed != sorted(expected_failures):
        raise RuntimeError(f"Gagal pada {failed}; seharusnya {expected_failures}")
    expected_exit = 1 if expected_failures else 0
    if actual_exit != expected_exit or summary["sloPassed"] != (expected_exit == 0):
        raise RuntimeError(f"Exit/status SLO tidak sesuai: exit={actual_exit}")
    # Negative control checkout harus mendekati injeksi 10%, bukan gagal total.
    if summary["selected"] == "error-rate" and expected_failures:
        if not 5 <= summary["errorRate"] <= 15:
            raise RuntimeError("Error rate di luar rentang demonstrasi 5..15%")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("case", choices=["load", "stress", "throughput", "latency", "error-rate"])
    parser.add_argument("--value")
    parser.add_argument("--expect-fail", default="", help="Nama check, dipisahkan koma")
    options = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    os.chdir(root)
    log_dir = root / "logs"
    log_dir.mkdir(exist_ok=True)
    existing = set((root / "reports").glob("*/summary.json"))
    command = ["dotnet", "PerformanceTests/bin/Release/net10.0/PerformanceTests.dll", options.case]
    if options.value is not None:
        command.append(options.value)
    expected = [item for item in options.expect_fail.split(",") if item]

    # Tolak port yang sudah dipakai agar tidak menguji server yang keliru.
    import socket
    with socket.socket() as probe:
        try:
            probe.bind(("127.0.0.1", 5080))
        except OSError as error:
            raise RuntimeError("Port 5080 sudah dipakai; hentikan API sebelumnya") from error

    with (log_dir / "api.log").open("w", encoding="utf-8") as api_log:
        api = subprocess.Popen(
            ["dotnet", "DemoApi/bin/Release/net10.0/DemoApi.dll", "--urls", "http://127.0.0.1:5080"],
            stdout=api_log, stderr=subprocess.STDOUT,
        )
        try:
            # Tidak memakai proxy untuk health check localhost.
            opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
            for _ in range(60):
                if api.poll() is not None:
                    raise RuntimeError("API berhenti; baca logs/api.log")
                try:
                    with opener.open("http://127.0.0.1:5080/health", timeout=1) as response:
                        if response.status == 200:
                            break
                except (urllib.error.URLError, TimeoutError):
                    pass
                time.sleep(0.5)
            else:
                raise RuntimeError("API belum siap setelah sekitar 30 detik")

            with (log_dir / "nbomber.log").open("w", encoding="utf-8") as test_log:
                result = subprocess.run(command, stdout=test_log, stderr=subprocess.STDOUT, timeout=300)
            print((log_dir / "nbomber.log").read_text(encoding="utf-8", errors="replace"))
            fresh = set((root / "reports").glob("*/summary.json")) - existing
            if len(fresh) != 1:
                raise RuntimeError("Runner tidak menghasilkan tepat satu summary baru")
            summary = json.loads(fresh.pop().read_text(encoding="utf-8"))
            if summary["selected"] != options.case:
                raise RuntimeError("Summary berasal dari kasus yang berbeda")
            verify(summary, result.returncode, expected)
            message = "SLO FAIL sesuai rancangan latihan" if expected else "SLO PASS"
            print(f"CI PASS: {message}")
            if os.environ.get("GITHUB_STEP_SUMMARY"):
                with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
                    output.write(
                        f"### {options.case}: {message}\n\n"
                        f"| OK RPS | p95 (ms) | p99 (ms) | Error (%) |\n|---|---|---|---|\n"
                        f"| {summary['successfulRps']:.2f} | {summary['p95']:.2f} | "
                        f"{summary['p99']:.2f} | {summary['errorRate']:.2f} |\n"
                    )
        finally:
            if api.poll() is None:
                api.terminate()
                try:
                    api.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    api.kill()
                    api.wait(timeout=10)


if __name__ == "__main__":
    main()
```
<!-- /source:scripts/ci_test.py -->

## 16. Referensi resmi

- [NBomber: overview](https://nbomber.com/docs/getting-started/overview/) — gambaran framework.
- [NBomber: instalasi](https://nbomber.com/docs/getting-started/installation/) — penggunaan library pada proyek .NET.
- [NBomber 6.7.1 di NuGet](https://www.nuget.org/packages/NBomber/6.7.1) — versi paket contoh.
- [Load Simulation](https://nbomber.com/docs/nbomber/load-simulation/) — model beban dan simulasi.
- [Scenario](https://nbomber.com/docs/nbomber/scenario/) — warm-up dan konfigurasi scenario.
- [Step](https://nbomber.com/docs/nbomber/scenario/step/) — pengukuran operasi dalam alur yang lebih panjang.
- [Reports](https://nbomber.com/docs/reporting/reports/) — format laporan dan metrik.
- [Asserts and Thresholds](https://nbomber.com/docs/nbomber/asserts_and_thresholds/) — definisi kriteria kualitas.
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — prasyarat build dan runtime.
- [GitHub Actions: checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), [setup-python](https://github.com/actions/setup-python), [upload-artifact](https://github.com/actions/upload-artifact) — action yang dipakai workflow.

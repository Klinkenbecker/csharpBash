# docs/perf-history.tsv -> docs/perf-history.svg (tools/perf-history.sh runs it): C#Bash's advantage, times faster
# than Git Bash and than WSL bash, per benchmark row, at each published build. Two graphs of eight small panels, each
# panel from 0 so a dip is not exaggerated; first and last values labeled; a <title> on every point (shown on hover
# when the SVG is opened on its own). One hue on a light surface of its own, so it reads on a dark page too.
BEGIN { FS = "\t"; nb = 0; nr = 0 }
NR == 1 { next }
{
	if (!($1 in bSeen)) { bSeen[$1] = 1; builds[++nb] = $1 }
	if (!($2 in rSeen)) { rSeen[$2] = 1; rows[++nr] = $2 }
	cs[$1, $2] = $3; gb[$1, $2] = $4; ws[$1, $2] = $5
}
function niceMax(v,   p) {
	p = 1; while (p * 10 <= v) p *= 10
	if (v <= p * 1.5) return p * 1.5; if (v <= p * 2) return p * 2; if (v <= p * 3) return p * 3
	if (v <= p * 5) return p * 5; if (v <= p * 7.5) return p * 7.5; return p * 10
}
function fmt(v) { return v >= 100 ? sprintf("%.0f", v) : (v >= 10 ? sprintf("%.1f", v) : sprintf("%.2f", v)) }
function ratio(which, b, r) { return (which == "git" ? gb[b, r] : ws[b, r]) / cs[b, r] }
function graph(which, top, title,   i, j, r, x0, y0, ymax, v, x, y, pts, col, rowi, lx, ly, ex, ey) {
	printf "<text class=\"h2\" x=\"20\" y=\"%d\">%s</text>\n", top, title
	for (j = 1; j <= nr; j++) {
		r = rows[j]; col = (j - 1) % 4; rowi = int((j - 1) / 4)
		x0 = 50 + col * (PW + 40); y0 = top + 40 + rowi * (PH + 64)
		ymax = 0
		for (i = 1; i <= nb; i++) { v = ratio(which, builds[i], r); if (v > ymax) ymax = v }
		ymax = niceMax(ymax)
		printf "<text class=\"pt\" x=\"%d\" y=\"%d\">%s</text>\n", x0, y0 - 16, r
		printf "<line class=\"grid\" x1=\"%d\" x2=\"%d\" y1=\"%d\" y2=\"%d\"/>", x0, x0 + PW, y0, y0
		printf "<line class=\"grid\" x1=\"%d\" x2=\"%d\" y1=\"%d\" y2=\"%d\"/>\n", x0, x0 + PW, y0 + PH, y0 + PH
		printf "<text class=\"ax\" x=\"%d\" y=\"%d\" text-anchor=\"end\">%s×</text>", x0 - 4, y0 + 4, fmt(ymax)
		printf "<text class=\"ax\" x=\"%d\" y=\"%d\" text-anchor=\"end\">0</text>\n", x0 - 4, y0 + PH + 4
		printf "<text class=\"ax\" x=\"%d\" y=\"%d\">%s</text>", x0, y0 + PH + 15, builds[1]
		printf "<text class=\"ax\" x=\"%d\" y=\"%d\" text-anchor=\"end\">%s</text>\n", x0 + PW, y0 + PH + 15, builds[nb]
		pts = ""
		for (i = 1; i <= nb; i++) {
			v = ratio(which, builds[i], r)
			x = x0 + (i - 1) * PW / (nb - 1); y = y0 + PH - v / ymax * PH
			pts = pts sprintf("%.1f,%.1f ", x, y)
			if (i == 1) { lx = x; ly = y } else if (i == nb) { ex = x; ey = y }
		}
		printf "<polyline class=\"line\" points=\"%s\"/>\n", pts
		for (i = 1; i <= nb; i++) {
			v = ratio(which, builds[i], r)
			x = x0 + (i - 1) * PW / (nb - 1); y = y0 + PH - v / ymax * PH
			printf "<circle class=\"dot\" cx=\"%.1f\" cy=\"%.1f\" r=\"3.5\"><title>%s, build %s (hg rev %s): %s× faster (C#Bash %s s, %s %s s)</title></circle>\n", x, y, r, builds[i], builds[i], fmt(v), cs[builds[i], r], (which == "git" ? "Git Bash" : "WSL"), (which == "git" ? gb[builds[i], r] : ws[builds[i], r])
		}
		printf "<text class=\"val\" x=\"%.1f\" y=\"%.1f\">%s×</text>", lx + 2, ly - 7, fmt(ratio(which, builds[1], r))
		printf "<text class=\"val b\" x=\"%.1f\" y=\"%.1f\" text-anchor=\"end\">%s×</text>\n", ex - 2, ey - 7, fmt(ratio(which, builds[nb], r))
	}
}
END {
	PW = 220; PH = 100; W = 4 * (PW + 40) + 40; GH = 2 * (PH + 64) + 40; H = 2 * GH + 70
	printf "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 %d %d\" width=\"%d\" height=\"%d\" role=\"img\" aria-label=\"C#Bash advantage per published build\">\n", W, H, W, H
	printf "<title>C#Bash: times faster than Git Bash and than WSL bash, per benchmark row, at each published build (%s to %s)</title>\n", builds[1], builds[nb]
	print "<style>text{font-family:system-ui,Segoe UI,sans-serif;fill:#0b0b0b}.h1{font-size:17px;font-weight:600}.h2{font-size:14px;font-weight:600}.sub{font-size:11px;fill:#52514e}.pt{font-size:12px;font-weight:600}.ax{font-size:9px;fill:#8a8984}.val{font-size:10px;fill:#52514e}.b{font-weight:600;fill:#0b0b0b}.grid{stroke:#e4e3df;stroke-width:1}.line{fill:none;stroke:#2a78d6;stroke-width:2;stroke-linejoin:round}.dot{fill:#2a78d6;stroke:#fcfcfb;stroke-width:1.5}</style>"
	printf "<rect width=\"%d\" height=\"%d\" fill=\"#fcfcfb\"/>\n", W, H
	printf "<text class=\"h1\" x=\"20\" y=\"28\">C#Bash's advantage, per published build (%s to %s)</text>\n", builds[1], builds[nb]
	print "<text class=\"sub\" x=\"20\" y=\"46\">Times faster than the other shell on each benchmark row (higher is better); each panel starts at 0×.</text>"
	graph("git", 80, "Against Git Bash 4.4 (Git for Windows)")
	graph("wsl", 80 + GH, "Against WSL bash 5.1 (native Linux bash)")
	print "</svg>"
}

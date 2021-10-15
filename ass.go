package main

import (
	"encoding/binary"
	"fmt"
	"github.com/antchfx/xmlquery"
	"github.com/asticode/go-astisub"
	"io"
	"log"
	"os"
	"path"
	"regexp"
	"strconv"
	"strings"
	"sync"
)

const (
	ttx        = "ttx"
	pyftsubset = "pyftsubset"
)

type fontInfo struct {
	file    string
	str     string
	index   string
	oldName string
	newName string
	ttx     string
	sFont   string
}

type ass struct {
	files     []string
	_fonts    string
	output    string
	m         map[string]*fontInfo
	fonts     []string
	sFonts    []string
	subtitles map[string]string
}

func (self *ass) parse() bool {
	ec := 0
	self.subtitles = make(map[string]string)
	for _, file := range self.files {
		f, err := openFile(file, true, false)
		if err != nil {
			ec++
		} else {
			data, _ := io.ReadAll(f)
			str := string(data)
			if err == nil {
				self.subtitles[file] = str
			} else {
				ec++
			}
		}
		if ec > 0 {
			log.Printf(`Failed to read the ass file: "%s"`, file)
		}
	}
	if ec == 0 {
		reg, _ := regexp.Compile(`\{?\\fn@?([^\\]+)[\\\}]`)
		m := make(map[string]map[rune]bool)
		for k, v := range self.subtitles {
			subtitle, err := astisub.ReadFromSSA(strings.NewReader(v))
			if err != nil {
				ec++
				log.Printf(`Failed to read the ass file: "%s"`, k)
				continue
			}
			for _, item := range subtitle.Items {
				for _, _item := range item.Lines {
					for _, __item := range _item.Items {
						name := item.Style.InlineStyle.SSAFontName
						if __item.InlineStyle != nil {
							arr := reg.FindStringSubmatch(__item.InlineStyle.SSAEffect)
							if len(arr) > 1 {
								name = arr[1]
							}
						}
						if strings.HasPrefix(name, "@") && len(name) > 1 {
							name = name[1:]
						}
						if m[name] == nil {
							m[name] = make(map[rune]bool)
						}
						str := __item.Text
						for _, char := range str {
							m[name][char] = true
						}
					}
				}
			}
		}
		self.m = make(map[string]*fontInfo)
		reg, _ = regexp.Compile("[A-Za-z0-9]]")
		for k, v := range m {
			str := ""
			for _k, _ := range v {
				str += string(_k)
			}
			str = strings.TrimSpace(str)
			str = reg.ReplaceAllString(str, "")
			str += "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
			reg, _ = regexp.Compile("[１２３４５６７８９０]")
			if reg.MatchString(str) {
				str = reg.ReplaceAllString(str, "")
				str += "１２３４５６７８９０"
			}
			if str != "" {
				self.m[k] = new(fontInfo)
				self.m[k].str = str
				self.m[k].oldName = k
			}
		}
	}
	if len(self.m) == 0 {
		log.Printf(`Not Found item in the ass file(s): "%d"`, len(self.files))
	}
	return ec == 0
}

func (self *ass) getTTCCount(file string) int {
	f, err := openFile(file, true, false)
	if err == nil {
		defer func() { _ = f.Close() }()
		data := make([]byte, 4)
		if n, err := f.ReadAt(data, 8); err == nil && n == 4 {
			return int(binary.BigEndian.Uint32(data))
		}
	}
	return 0
}

func (self *ass) dumpFont(file string, full bool) bool {
	ok := false
	count := 1
	_, n, _, _ := splitPath(file)
	if strings.HasSuffix(file, ".ttc") {
		count = self.getTTCCount(file)
		if count < 1 {
			log.Printf(`Failed to get the ttc font count: "%s".`, n)
			return ok
		}
	}
	for i := 0; i < count; i++ {
		fn := fmt.Sprintf("%s_%d.ttx", file, i)
		args := make([]string, 0)
		args = append(args, "-q")
		args = append(args, "-f")
		args = append(args, "-y", strconv.Itoa(i))
		args = append(args, "-o", fn)
		if !full {
			args = append(args, "-t", "name")
		}
		args = append(args, file)
		if p, err := newProcess(nil, nil, nil, "", ttx, args...); err == nil {
			s, err := p.Wait()
			ok = err == nil && s.ExitCode() == 0
		}
		if !ok {
			log.Printf(`Failed to dump font(%t): "%s"[%d].`, full, n, i)
		}
	}
	return ok
}

func (self *ass) dumpFonts(files []string, full bool) bool {
	ok := 0
	l := len(files)
	wg := new(sync.WaitGroup)
	wg.Add(l)
	m := new(sync.Mutex)
	for _, item := range files {
		go func(_item string) {
			_ok := self.dumpFont(_item, full)
			if _ok {
				m.Lock()
				ok++
				m.Unlock()
			}
			wg.Done()
		}(item)
	}
	wg.Wait()
	return ok == l
}

func (self *ass) getFontName(p string) []string {
	f, err := openFile(p, true, false)
	if err == nil {
		defer func() { _ = f.Close() }()
		names := make([]string, 0)
		if xml, err := xmlquery.Parse(f); err == nil {
			for _, v := range xml.SelectElements(`ttFont/name/namerecord[@platformID=3]`) {
				id := v.SelectAttr("nameID")
				name := strings.TrimSpace(v.FirstChild.Data)
				switch id {
				case "1":
					names = append(names, name)
					break
				case "4":
					names = append(names, name)
					break
				}
			}
		}
		return names
	}
	return nil
}

func (self *ass) getFontsName() map[string][]string {
	files, _ := findPath(self._fonts, `\.ttx$`)
	l := len(files)
	wg := new(sync.WaitGroup)
	wg.Add(l)
	m := new(sync.Mutex)
	_m := make(map[string][]string)
	for _, item := range files {
		go func(_item string) {
			names := self.getFontName(_item)
			if len(names) > 0 {
				m.Lock()
				_m[_item] = names
				m.Unlock()
			}
			wg.Done()
		}(item)
	}
	wg.Wait()
	return _m
}

func (self *ass) matchFonts() bool {
	if !self.dumpFonts(self.fonts, false) {
		return false
	}
	m := self.getFontsName()
	if len(m) > 0 {
		reg, _ := regexp.Compile(`_(\d+)\.ttx$`)
		for k, _ := range self.m {
			for _k, v := range m {
				for _, _v := range v {
					if _v == k {
						self.m[k].file = reg.ReplaceAllString(_k, "")
						self.m[k].ttx = _k
						self.m[k].index = reg.FindStringSubmatch(_k)[1]
						self.m[k].newName = randomStr(8)
						break
					}
				}
			}
		}
	}
	ok := true
	for _, v := range self.m {
		if v.file == "" {
			ok = false
			log.Printf(`Missing the font: "%s".`, v.oldName)
		}
	}
	return ok
}

func (self *ass) createFontSubset(font *fontInfo) bool {
	ok := false
	fn := fmt.Sprintf(`%s.txt`, font.file)
	_, n, e, ne := splitPath(font.file)
	if e == ".ttc" {
		e = ".ttf"
	}
	if os.MkdirAll(self.output, os.ModePerm) != nil {
		log.Println("Failed to create the output folder.")
		return false
	}
	if os.WriteFile(fn, []byte(font.str), os.ModePerm) == nil {
		_fn := fmt.Sprintf("%s.%s%s", ne, font.newName, e)
		_fn = path.Join(self.output, _fn)
		args := make([]string, 0)
		args = append(args, "--text-file="+fn)
		args = append(args, "--output-file="+_fn)
		args = append(args, "--name-languages="+"*")
		args = append(args, "--font-number="+font.index)
		args = append(args, font.file)
		if p, err := newProcess(nil, nil, nil, "", pyftsubset, args...); err == nil {
			s, err := p.Wait()
			ok = err == nil && s.ExitCode() == 0
		}
		if !ok {
			log.Printf(`Failed to subset font: "%s"[%s].`, n, font.index)
		} else {
			font.sFont = _fn
		}

	} else {
		log.Printf(`Failed to write the font text: "%s".`, n)
	}
	return ok
}

func (self *ass) createFontsSubset() bool {
	err := os.RemoveAll(self.output)
	if !(err == nil || err == os.ErrNotExist) {
		log.Println("Failed to clean the output folder.")
		return false
	}
	ok := 0
	l := len(self.m)
	wg := new(sync.WaitGroup)
	wg.Add(l)
	m := new(sync.Mutex)
	for _, item := range self.m {
		go func(_item *fontInfo) {
			_ok := self.createFontSubset(_item)
			if _ok {
				m.Lock()
				ok++
				m.Unlock()
			}
			wg.Done()
		}(item)
	}
	wg.Wait()
	return ok == l
}

func (self *ass) changeFontName(font *fontInfo) bool {
	ec := 0
	if self.dumpFont(font.sFont, true) {
		fn := fmt.Sprintf("%s_0.ttx", font.sFont)
		f, err := openFile(fn, true, false)
		if err == nil {
			defer func() {
				_ = f.Close()
				_ = os.Remove(fn)
			}()
			if xml, err := xmlquery.Parse(f); err == nil {
				for _, v := range xml.SelectElements(`ttFont/name/namerecord`) {
					id := v.SelectAttr("nameID")
					switch id {
					case "0":
						v.FirstChild.Data = "Processed by " + pName
						break
					case "1", "3", "4", "6":
						v.FirstChild.Data = font.newName
						break
					}
				}
				str := `<?xml version="1.0" encoding="UTF-8"?>`
				str += xml.SelectElement("ttFont").OutputXML(true)
				if os.WriteFile(fn, []byte(str), os.ModePerm) == nil {
					args := make([]string, 0)
					args = append(args, "-q")
					args = append(args, "-f")
					args = append(args, "-o", font.sFont)
					args = append(args, fn)
					ok := false
					if p, err := newProcess(os.Stdin, nil, nil, "", ttx, args...); err == nil {
						s, err := p.Wait()
						ok = err == nil && s.ExitCode() == 0
					}
					if !ok {
						ec++
						_, n, _, _ := splitPath(font.sFont)
						log.Printf(`Failed to compile the font: "%s".`, n)
					}
				}
			} else {
				log.Printf(`Faild to change the font name: "%s".`, font.oldName)
			}
		}
	}
	return ec == 0
}

func (self *ass) changeFontsName() bool {
	ok := 0
	l := len(self.m)
	wg := new(sync.WaitGroup)
	wg.Add(l)
	m := new(sync.Mutex)
	for _, item := range self.m {
		go func(_item *fontInfo) {
			_ok := self.changeFontName(_item)
			if _ok {
				m.Lock()
				ok++
				m.Unlock()
			}
			wg.Done()
		}(item)
	}
	wg.Wait()
	return ok == l
}

func (self *ass) replaceFontNameInAss() bool {
	ec := 0
	m := make(map[string]map[string]bool)
	for _, v := range self.m {
		for f, s := range self.subtitles {
			if m[f] == nil {
				m[f] = make(map[string]bool)
			}
			n := regexp.QuoteMeta(v.oldName)
			reg, _ := regexp.Compile(fmt.Sprintf(`(Style:[^,\n]+,|\\fn)(@?)%s`, n))
			if reg.MatchString(s) {
				s = reg.ReplaceAllString(s, "${1}${2}"+v.newName)
				m[f][v.oldName] = true
				self.subtitles[f] = s
			}
		}
	}
	for f, s := range self.subtitles {
		comments := make([]string, 0)
		comments = append(comments, "[script info]")
		comments = append(comments, "; ----- Font subset begin -----")
		for k, _ := range m[f] {
			comments = append(comments, fmt.Sprintf("; Font subset: %s - %s", self.m[k].newName, k))
		}
		if len(comments) > 2 {
			comments = append(comments, "; Processed by "+pName)
			comments = append(comments, "; -----  Font subset end  -----")
			comments = append(comments, "")
			s = strings.Replace(s, "[Script Info]\r\n", strings.Join(comments, "\r\n"), 1)
			_, n, _, _ := splitPath(f)
			fn := path.Join(self.output, n)
			ok := false
			if os.WriteFile(fn, []byte(s), os.ModePerm) == nil {
				ok = true
			} else {
				ec++
			}
			if !ok {
				log.Printf(`Failed to write the new ass file: "%s".`, fn)
			}
		}
	}
	return ec == 0
}

func genASSes(files []string, fonts, output string) bool {
	if len(files) == 0 {
		return false
	}
	obj := new(ass)
	obj.files = files
	obj._fonts = fonts
	obj.output = output

	d, _, _, _ := splitPath(obj.files[0])
	if obj._fonts == "" {
		obj._fonts += path.Join(d, "fonts")
	}
	if obj.output == "" {
		obj.output += path.Join(d, "output")
	}

	obj.fonts = findFonts(obj._fonts)

	return obj.parse() && obj.matchFonts() && obj.createFontsSubset() && obj.changeFontsName() && obj.replaceFontNameInAss()
}
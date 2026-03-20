BEGIN { printf "{\"enc\":false,\"content\":[" }
{
  gsub(/\\/, "\\\\")
  gsub(/"/, "\\\"")
  gsub(/\t/, "\\t")
  sub(/\r$/, "")
  if (NR > 1) printf ","
  printf "\"%s\"", $0
}
END { printf "]}" }
